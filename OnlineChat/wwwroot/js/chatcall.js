// chatcall.js
// Отдельное WS-подключение к CallHub (не путать с connection из chat.js, который для чата).
// Использует те же chatData.roomId / chatData.username, что уже загружены в chat.html.

(function () {
    "use strict";
    const MIME_TYPE = "audio/webm;codecs=opus";
    const CHUNK_MS = 250;

    const callConnection = new signalR.HubConnectionBuilder()
        .withUrl("/callHub")
        .configureLogging(signalR.LogLevel.Information)
        .withAutomaticReconnect()
        .build();

    let mediaRecorder = null;
    let localStream = null;
    let inCall = false;

// userName -> { mediaSource, sourceBuffer, audioEl, pendingChunks: [] }
    const remotePlayers = new Map();

// ---------- base64 <-> ArrayBuffer ----------
// SignalR с дефолтным JSON-протоколом сериализует byte[] на C# как base64-строку
// (и ожидает то же самое на вход) — сырой Uint8Array/ArrayBuffer JSON.stringify
// превращает в объект с числовыми ключами, а не в валидный byte[], отсюда и ошибка биндинга.
    function arrayBufferToBase64(buffer) {
        let binary = "";
        const bytes = new Uint8Array(buffer);
        const chunkSize = 0x8000; // обходим лимит на количество аргументов String.fromCharCode.apply
        for (let i = 0; i < bytes.length; i += chunkSize) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
        }
        return btoa(binary);
    }

    function base64ToArrayBuffer(base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return bytes.buffer;
    }

// ---------- UI ----------
// Кнопка звонка — рядом с "Покинуть комнату" в шапке чата
    const callBtn = document.createElement("button");
    callBtn.id = "call-btn";
    callBtn.textContent = "📞 Позвонить";
    document.getElementById("header-controls").prepend(callBtn);

// Панель участников звонка (список + индикатор состояния)
    const callBar = document.createElement("div");
    callBar.id = "call-bar";
    callBar.style.cssText = "display:none; padding:8px 15px; background:#eef; border-bottom:1px solid #ccd; font-size:14px;";
    callBar.innerHTML = `В звонке: <span id="call-participants"></span>`;
    document.getElementById("chat-header").insertAdjacentElement("afterend", callBar);

    callBtn.addEventListener("click", () => {
        if (inCall) {
            leaveCall();
        } else {
            joinCall();
        }
    });

// ---------- Подключение к CallHub ----------
    async function startCallConnection() {
        try {
            await callConnection.start();
            console.log("Connected to CallHub");
        } catch (err) {
            console.error("Ошибка подключения к CallHub:", err);
            setTimeout(startCallConnection, 3000);
        }
    }

    callConnection.onclose(() => {
        console.log("CallHub connection closed");
    });

    callConnection.onreconnected(async () => {
        if (inCall) {
            // после реконнекта нужно заново зайти в комнату звонка
            await callConnection.invoke("JoinCall", chatData.roomId, chatData.username);
        }
    });

// ---------- Обработчики событий звонка ----------
    callConnection.on("CallJoined", (existingUserNames) => {
        existingUserNames.forEach((userName) => createRemotePlayer(userName));
        updateParticipantsUi();
    });

    callConnection.on("CallUserJoined", (userName) => {
        createRemotePlayer(userName);
        updateParticipantsUi();
        addMessage("System", `${userName} присоединился к звонку`, "system");
    });

    callConnection.on("CallUserLeft", (userName) => {
        destroyRemotePlayer(userName);
        updateParticipantsUi();
        addMessage("System", `${userName} вышел из звонка`, "system");
    });

    callConnection.on("ReceiveAudioChunk", (userName, chunk) => {
        const player = remotePlayers.get(userName);
        if (!player) return;

        // byte[] на C# приходит в JSON-протоколе SignalR как base64-строка (как и в chatfiles.js)
        const data = base64ToArrayBuffer(chunk);
        player.pendingChunks.push(data);
        flushPendingChunks(player);
    });

// ---------- Вход/выход из звонка ----------
    async function joinCall() {
        try {
            localStream = await navigator.mediaDevices.getUserMedia({
                audio: { echoCancellation: true, noiseSuppression: true },
            });

            mediaRecorder = new MediaRecorder(localStream, { mimeType: MIME_TYPE });
            mediaRecorder.ondataavailable = async (event) => {
                if (event.data.size > 0 && inCall) {
                    const buffer = await event.data.arrayBuffer();
                    // byte[] на сервере ждёт base64-строку в JSON-протоколе SignalR, не сырой Uint8Array
                    await callConnection.invoke("SendAudioChunk", chatData.roomId, arrayBufferToBase64(buffer));
                }
            };
            mediaRecorder.start(CHUNK_MS);

            await callConnection.invoke("JoinCall", chatData.roomId, chatData.username);

            inCall = true;
            callBtn.textContent = "📵 Завершить звонок";
            callBar.style.display = "block";
            addMessage("System", "Вы вошли в звонок", "system");
        } catch (err) {
            console.error("Не удалось начать звонок:", err);
            alert("Не удалось получить доступ к микрофону");
        }
    }

    async function leaveCall() {
        try {
            await callConnection.invoke("LeaveCall", chatData.roomId);
        } catch (err) {
            console.error(err);
        }

        mediaRecorder?.stop();
        mediaRecorder = null;
        localStream?.getTracks().forEach((t) => t.stop());
        localStream = null;

        remotePlayers.forEach((_, userName) => destroyRemotePlayer(userName));

        inCall = false;
        callBtn.textContent = "📞 Позвонить";
        callBar.style.display = "none";
        addMessage("System", "Вы вышли из звонка", "system");
    }

// Если пользователь покидает комнату чата целиком — выходим и из звонка.
// leaveBtn определяется в inline-скрипте страницы, который может грузиться позже,
// поэтому вешаем обработчик через DOMContentLoaded/сразу, если DOM уже готов.
    function attachLeaveBtnHandler() {
        document.getElementById("leave-btn")?.addEventListener("click", () => {
            if (inCall) leaveCall();
        });
    }
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", attachLeaveBtnHandler);
    } else {
        attachLeaveBtnHandler();
    }

    window.addEventListener("beforeunload", () => {
        if (inCall) callConnection.invoke("LeaveCall", chatData.roomId).catch(() => {});
    });

// ---------- Воспроизведение голоса участников ----------
    function createRemotePlayer(userName) {
        if (remotePlayers.has(userName)) return;

        const audioEl = document.createElement("audio");
        audioEl.autoplay = true;
        audioEl.dataset.userName = userName;

        const mediaSource = new MediaSource();
        audioEl.src = URL.createObjectURL(mediaSource);
        document.body.appendChild(audioEl);

        const player = { mediaSource, sourceBuffer: null, audioEl, pendingChunks: [] };

        mediaSource.addEventListener("sourceopen", () => {
            player.sourceBuffer = mediaSource.addSourceBuffer(MIME_TYPE);
            player.sourceBuffer.addEventListener("updateend", () => flushPendingChunks(player));
        });

        remotePlayers.set(userName, player);
    }

    function destroyRemotePlayer(userName) {
        const player = remotePlayers.get(userName);
        if (!player) return;
        player.audioEl.remove();
        remotePlayers.delete(userName);
    }

    function flushPendingChunks(player) {
        if (!player.sourceBuffer || player.sourceBuffer.updating || player.pendingChunks.length === 0) return;
        const chunk = player.pendingChunks.shift();
        try {
            player.sourceBuffer.appendBuffer(chunk);
        } catch (err) {
            console.error("Ошибка appendBuffer:", err);
        }
    }

    function updateParticipantsUi() {
        const names = [chatData.username, ...Array.from(remotePlayers.keys())];
        document.getElementById("call-participants").textContent = names.join(", ");
    }

    startCallConnection();

})();