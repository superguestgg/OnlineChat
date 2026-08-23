// chatcall.js
// Отдельное WS-подключение к CallHub (не путать с connection из chat.js, который для чата).
// Использует те же chatData.roomId / chatData.username, что уже загружены в chat.html.

(function () {
    "use strict";

    // Кандидаты кодеков в порядке предпочтения. Safari/iOS не поддерживает WebM вообще
    // (ни на запись через MediaRecorder, ни на воспроизведение через MediaSource) —
    // там обычно доступен только audio/mp4 (AAC). Выбираем первый поддерживаемый браузером.
    const MIME_CANDIDATES = [
        "audio/webm;codecs=opus",
        "audio/mp4",
    ];

    function pickSupportedMimeType() {
        if (typeof MediaRecorder === "undefined") return null;
        return MIME_CANDIDATES.find((type) => MediaRecorder.isTypeSupported(type)) || null;
    }

    const MIME_TYPE = pickSupportedMimeType();
    const CHUNK_MS = 250;

    const callConnection = new signalR.HubConnectionBuilder()
        .withUrl("/callHub")
        .configureLogging(signalR.LogLevel.Information)
        .withAutomaticReconnect()
        .build();

    let mediaRecorder = null;
    let localStream = null;
    let inCall = false;

    // connectionId -> { userName, mediaSource, sourceBuffer, audioEl, pendingChunks: [] }
    // Ключ — именно connectionId (стабильно уникален), а НЕ userName: два разных
    // подключения могут иметь одинаковое отображаемое имя, и если бы маршрутизация
    // шла по имени, их аудио-потоки (два разных WebM-контейнера) писались бы в один
    // и тот же SourceBuffer и ломали бы декодирование.
    const remotePlayers = new Map();

    // ---------- base64 <-> ArrayBuffer ----------
    function arrayBufferToBase64(buffer) {
        let binary = "";
        const bytes = new Uint8Array(buffer);
        const chunkSize = 0x8000;
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
    const callBtn = document.createElement("button");
    callBtn.id = "call-btn";
    callBtn.textContent = "📞 Позвонить";
    document.getElementById("header-controls").prepend(callBtn);

    // Мьют микрофона — отключает отправку своего звука остальным участникам
    const muteMicBtn = document.createElement("button");
    muteMicBtn.id = "mute-mic-btn";
    muteMicBtn.textContent = "🎤";
    muteMicBtn.title = "Выключить микрофон";

    // Мьют динамика ("глухота") — отключает воспроизведение звука от остальных участников,
    // при этом сам продолжаешь им что-то отправлять — удобно для теста в одном браузере
    const muteSpeakerBtn = document.createElement("button");
    muteSpeakerBtn.id = "mute-speaker-btn";
    muteSpeakerBtn.textContent = "🔊";
    muteSpeakerBtn.title = "Выключить звук (глухой режим)";

    let micMuted = false;
    let speakerMuted = false;

    const callBar = document.createElement("div");
    callBar.id = "call-bar";
    callBar.style.cssText = "display:none; padding:8px 15px; background:#eef; border-bottom:1px solid #ccd; font-size:14px; align-items:center; gap:10px;";
    callBar.style.display = "none";
    callBar.append(
        Object.assign(document.createElement("span"), { textContent: "В звонке: " }),
        Object.assign(document.createElement("span"), { id: "call-participants" }),
        muteMicBtn,
        muteSpeakerBtn
    );
    document.getElementById("chat-header").insertAdjacentElement("afterend", callBar);

    callBtn.addEventListener("click", () => {
        if (inCall) {
            leaveCall();
        } else {
            joinCall();
        }
    });

    muteMicBtn.addEventListener("click", () => {
        micMuted = !micMuted;
        // enabled=false у аудио-трека — трек продолжает существовать (MediaRecorder не рвётся),
        // но реально не пишет звук: собеседникам будет уходить тишина, а не ошибка
        localStream?.getAudioTracks().forEach((track) => (track.enabled = !micMuted));
        muteMicBtn.textContent = micMuted ? "🚫🎤" : "🎤";
        muteMicBtn.title = micMuted ? "Включить микрофон" : "Выключить микрофон";
    });

    muteSpeakerBtn.addEventListener("click", () => {
        speakerMuted = !speakerMuted;
        // muted у каждого <audio> — сами данные продолжают приходить и буферизоваться,
        // просто не проигрываются. Применяем ко всем текущим плеерам и запоминаем
        // состояние для плееров, которые будут созданы позже (см. createRemotePlayer)
        remotePlayers.forEach((player) => (player.audioEl.muted = speakerMuted));
        muteSpeakerBtn.textContent = speakerMuted ? "🔇" : "🔊";
        muteSpeakerBtn.title = speakerMuted ? "Включить звук" : "Выключить звук (глухой режим)";
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
            await callConnection.invoke("JoinCall", chatData.roomId, chatData.username);
        }
    });

    // ---------- Обработчики событий звонка ----------
    callConnection.on("CallJoined", (existingParticipants) => {
        console.log("CallJoined, участники уже в звонке:", existingParticipants);
        existingParticipants.forEach(({ connectionId, userName }) => createRemotePlayer(connectionId, userName));
        updateParticipantsUi();
    });

    callConnection.on("CallUserJoined", (connectionId, userName) => {
        console.log("CallUserJoined:", userName, connectionId);
        createRemotePlayer(connectionId, userName);
        updateParticipantsUi();
        addMessage("System", `${userName} присоединился к звонку`, "system");
    });

    callConnection.on("CallUserLeft", (connectionId) => {
        const userName = remotePlayers.get(connectionId)?.userName ?? "Участник";
        destroyRemotePlayer(connectionId);
        updateParticipantsUi();
        addMessage("System", `${userName} вышел из звонка`, "system");
    });

    callConnection.on("ReceiveAudioChunk", (fromConnectionId, chunk) => {
        const player = remotePlayers.get(fromConnectionId);
        if (!player) {
            console.warn("Чанк от неизвестного участника (нет плеера):", fromConnectionId);
            return;
        }

        const data = base64ToArrayBuffer(chunk);
        player.pendingChunks.push(data);
        flushPendingChunks(player);
    });

    // ---------- Вход/выход из звонка ----------
    async function joinCall() {
        if (!MIME_TYPE) {
            alert(
                "Этот браузер не поддерживает запись аудио в форматах, которые умеет " +
                "воспроизводить наше приложение (webm/opus или mp4/aac). " +
                "Попробуй Chrome/Edge/Firefox на компьютере или Safari 14.3+ на iOS."
            );
            return;
        }

        try {
            localStream = await navigator.mediaDevices.getUserMedia({
                audio: { echoCancellation: true, noiseSuppression: true },
            });

            mediaRecorder = new MediaRecorder(localStream, { mimeType: MIME_TYPE });
            mediaRecorder.ondataavailable = async (event) => {
                if (event.data.size > 0 && inCall) {
                    const buffer = await event.data.arrayBuffer();
                    await callConnection.invoke("SendAudioChunk", chatData.roomId, arrayBufferToBase64(buffer));
                }
            };
            mediaRecorder.onerror = (event) => {
                console.error("MediaRecorder error:", event.error);
            };
            mediaRecorder.start(CHUNK_MS);

            await callConnection.invoke("JoinCall", chatData.roomId, chatData.username);

            inCall = true;
            callBtn.textContent = "📵 Завершить звонок";
            callBar.style.display = "flex";
            addMessage("System", "Вы вошли в звонок", "system");
        } catch (err) {
            console.error("Не удалось начать звонок:", err);
            if (err.name === "NotSupportedError") {
                alert("Браузер не поддерживает запись аудио в выбранном формате (" + MIME_TYPE + ")");
            } else if (err.name === "NotAllowedError") {
                alert("Доступ к микрофону не разрешён. Проверь разрешения сайта.");
            } else {
                alert("Не удалось начать звонок: " + err.message);
            }
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

        Array.from(remotePlayers.keys()).forEach((connectionId) => destroyRemotePlayer(connectionId));

        inCall = false;
        micMuted = false;
        speakerMuted = false;
        muteMicBtn.textContent = "🎤";
        muteMicBtn.title = "Выключить микрофон";
        muteSpeakerBtn.textContent = "🔊";
        muteSpeakerBtn.title = "Выключить звук (глухой режим)";
        callBtn.textContent = "📞 Позвонить";
        callBar.style.display = "none";
        addMessage("System", "Вы вышли из звонка", "system");
    }

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
    function createRemotePlayer(connectionId, userName) {
        if (remotePlayers.has(connectionId)) return;

        if (!MIME_TYPE || !window.MediaSource || !MediaSource.isTypeSupported(MIME_TYPE)) {
            console.warn(`Браузер не может воспроизвести формат ${MIME_TYPE} — участник ${userName} не будет слышен`);
            return;
        }

        const audioEl = document.createElement("audio");
        audioEl.autoplay = true;
        audioEl.muted = speakerMuted;
        audioEl.dataset.connectionId = connectionId;
        audioEl.dataset.userName = userName;

        const mediaSource = new MediaSource();
        audioEl.src = URL.createObjectURL(mediaSource);
        document.body.appendChild(audioEl);

        // autoplay иногда тихо блокируется браузером без единой ошибки — если так,
        // sourceopen у MediaSource может вообще не наступить, и чанки будут вечно
        // копиться в очереди без звука и без единого намёка в консоли, почему.
        // Форсируем .play() явно, чтобы увидеть блокировку, если она есть.
        audioEl.play().catch((err) => {
            console.warn(`Автовоспроизведение для ${userName} заблокировано браузером:`, err.name, err.message);
        });

        audioEl.addEventListener("error", () => {
            console.error(`Ошибка <audio> для ${userName}:`, audioEl.error);
        });

        const player = { userName, mediaSource, sourceBuffer: null, audioEl, pendingChunks: [] };

        mediaSource.addEventListener("sourceopen", () => {
            console.log(`MediaSource открылся для ${userName} (${connectionId})`);
            try {
                player.sourceBuffer = mediaSource.addSourceBuffer(MIME_TYPE);
                player.sourceBuffer.addEventListener("updateend", () => flushPendingChunks(player));
                // на случай, если чанки уже накопились в очереди, пока sourceopen ещё не срабатывал
                flushPendingChunks(player);
            } catch (err) {
                console.error(`Не удалось создать SourceBuffer для ${userName}:`, err);
            }
        });

        mediaSource.addEventListener("sourceclose", () => {
            console.warn(`MediaSource закрылся для ${userName} (${connectionId})`);
        });

        remotePlayers.set(connectionId, player);
    }

    function destroyRemotePlayer(connectionId) {
        const player = remotePlayers.get(connectionId);
        if (!player) return;
        player.audioEl.remove();
        remotePlayers.delete(connectionId);
    }

    function flushPendingChunks(player) {
        if (!player.mediaSource || player.mediaSource.readyState !== "open") {
            return;
        }
        if (!player.sourceBuffer || player.sourceBuffer.updating || player.pendingChunks.length === 0) return;

        const chunk = player.pendingChunks.shift();
        try {
            player.sourceBuffer.appendBuffer(chunk);
        } catch (err) {
            console.error("Ошибка appendBuffer, пересоздаю плеер:", err);
            const connectionId = player.audioEl?.dataset.connectionId;
            const userName = player.userName;
            if (connectionId) {
                destroyRemotePlayer(connectionId);
                createRemotePlayer(connectionId, userName);
            }
        }
    }

    function updateParticipantsUi() {
        const names = [chatData.username, ...Array.from(remotePlayers.values()).map((p) => p.userName)];
        document.getElementById("call-participants").textContent = names.join(", ");
    }

    startCallConnection();

})();