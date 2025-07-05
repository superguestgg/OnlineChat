
function uint8ToBase64(u8Arr) {
    const CHUNK_SIZE = 0x8000; // 32kb chunks
    let index = 0;
    const length = u8Arr.length;
    let result = '';
    let slice;
    while (index < length) {
        slice = u8Arr.subarray(index, Math.min(index + CHUNK_SIZE, length));
        result += String.fromCharCode.apply(null, slice);
        index += CHUNK_SIZE;
    }
    return btoa(result);
}

async function sendFile(file, fileName = null) {
    let sentBytes = 0;
    if (fileName == null) {
        fileName = file.name;
    }

    const fileId = crypto.randomUUID(); // или любое другое уникальное значение

    const chunkSize = 64 * 1024;
    const fileSize = file.size;
    console.log(fileSize)
    let offset = 0;

    fileInfo.style.display = "block";
    fileNameLabel.textContent = fileName;
    fileProgress.value = 0;
    filePercent.textContent = "0%";
    const observable = createFileObservable(file, chunkSize);

    await connection.stream("StreamFile", chatData.roomId, fileId, fileName, observable)
        .subscribe({
            next: chunk => {
                sentBytes += chunk;

                // Обновляем прогресс
                const percent = Math.floor((sentBytes / fileSize) * 100);
                fileProgress.value = percent;
                filePercent.textContent = percent + "%";

                console.log("Отправлен чанк:", chunk);
            }, complete: () => {
                console.log("Отправка файла завершена");
                fileInput.value = "";
                fileInfo.style.display = "none";
                fileProgress.value = 0;
                filePercent.textContent = "0%";
                addMessage('System', `Отправка файла ${fileName} завершена`, 'system')
            }, error: err => {
                console.error("Ошибка отправки:", err);
            }
        });
    console.log("sfeeg");
}

function bigIntToUint8Array(bigint, length = 32) {
    let hex = bigint.toString(16);
    if (hex.length % 2) hex = '0' + hex;

    // Преобразуем в байты
    let bytes = [];
    for (let i = 0; i < hex.length; i += 2) {
        bytes.push(parseInt(hex.slice(i, i + 2), 16));
    }

    // Приводим к нужной длине
    if (bytes.length > length) {
        bytes = bytes.slice(-length); // берём младшие байты
    } else if (bytes.length < length) {
        const padding = new Array(length - bytes.length).fill(0);
        bytes = padding.concat(bytes); // дополняем слева нулями
    }

    return new Uint8Array(bytes);
}


async function importAesKey(keyMaterial) {
    return await crypto.subtle.importKey(
        "raw",
        keyMaterial,
        { name: "AES-GCM" },
        false,
        ["encrypt", "decrypt"]
    );
}

async function getDecryptionKey() {
    const dhKeys = JSON.parse(sessionStorage.getItem('diffieHellmanSecret'));
    console.log(dhKeys)
    if (!dhKeys) {
        throw new Error("Защищенное соединение не установлено");
    }
    const keyMaterial = bigIntToUint8Array(dhKeys);
    

    const rawKey = bigIntToUint8Array(BigInt(dhKeys));
    console.log(keyMaterial)
    console.log(rawKey)
    return await importAesKey(keyMaterial);
}

async function encryptChunk(key, dataBuffer) {
    const iv = crypto.getRandomValues(new Uint8Array(12)); // 12 байт IV
    const encrypted = await crypto.subtle.encrypt(
        { name: "AES-GCM", iv: iv },
        key,
        dataBuffer
    );

    // Склеиваем IV и зашифрованные данные
    const result = new Uint8Array(iv.byteLength + encrypted.byteLength);
    result.set(iv, 0);
    result.set(new Uint8Array(encrypted), iv.byteLength);
    return result;
}

async function decryptChunk(aesKey, chunk) {
    const iv = chunk.slice(0, 12); // первые 12 байт — IV
    const data = chunk.slice(12);

    return new Uint8Array(await crypto.subtle.decrypt(
        {
            name: "AES-GCM",
            iv: iv,
        },
        aesKey,
        data
    ));
}

// Импортируем CryptoKey из Uint8Array
async function importAesKeyFromBigInt(bigint) {
    const keyBytes = bigIntToUint8Array(bigint);
    return await crypto.subtle.importKey(
        "raw",
        keyBytes,
        "AES-GCM",
        false,
        ["encrypt", "decrypt"]
    );
}

// 🧪 ТЕСТ
(async () => {
    const secret = 12345678901234567890n; // BigInt (можно из DH)
    const key = await importAesKeyFromBigInt(secret);

    const originalText = "Hello, encrypted world! 🔒";
    const plainBytes = new TextEncoder().encode(originalText);

    const encryptedChunk = await encryptChunk(key, plainBytes);
    const decryptedChunk = await decryptChunk(key, encryptedChunk);

    const decryptedText = new TextDecoder().decode(decryptedChunk);

    console.log("Original:", originalText);
    console.log("Decrypted:", decryptedText);
    console.log("✅ Успешно:", originalText === decryptedText);
})();

async function sendEncryptedFile(file, fileName = null) {
    let sentBytes = 0;
    if (fileName == null) {
        fileName = file.name;
    }
    const dhKeys = JSON.parse(sessionStorage.getItem('diffieHellmanSecret'));
    const fileNameEncrypted = await encryptMessage(fileName, dhKeys);

    const fileId = crypto.randomUUID(); // или любое другое уникальное значение

    const chunkSize = 64 * 1024;
    const fileSize = file.size;
    console.log(fileSize)
    let offset = 0;

    fileInfo.style.display = "block";
    fileNameLabel.textContent = fileName;
    fileProgress.value = 0;
    filePercent.textContent = "0%";

    
    const aesKey = await getDecryptionKey();
    // Шифруем сообщение

    const observable = createObservableFromEncryptedFile(file, aesKey, chunkSize);
    
    await connection.stream("StreamFile", chatData.roomId, fileId, fileNameEncrypted, observable)
        .subscribe({
            next: chunk => {
                sentBytes += chunk;

                // Обновляем прогресс
                const percent = Math.floor((sentBytes / fileSize) * 100);
                fileProgress.value = percent;
                filePercent.textContent = percent + "%";

                console.log("Отправлен чанк:", chunk);
            }, complete: () => {
                console.log("Отправка файла завершена");
                fileInput.value = "";
                fileInfo.style.display = "none";
                fileProgress.value = 0;
                filePercent.textContent = "0%";
                addMessage('System', `Отправка файла ${fileName} завершена`, 'system')
            }, error: err => {
                console.error("Ошибка отправки:", err);
            }
        });
    console.log("sfeeg");
}

function createDownloadProgressUI(fileName, userName) {
    const container = document.createElement("div");
    container.className = "file-download-progress";

    const label = document.createElement("div");
    label.textContent = `📥 Получение: ${fileName} от ${userName}`;

    const progress = document.createElement("div");
    progress.textContent = "0 KB загружено";

    container.appendChild(label);
    container.appendChild(progress);

    document.getElementById("messages").appendChild(container); // контейнер для сообщений

    return {
        element: container, progress: progress
    };
}


function createVideoModal(titleText, mediaElement, onActionClick, isVideoRecording = false) {
    const modal = document.createElement("div");
    modal.style = `
        position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%);
        background: white; border-radius: 8px; padding: 15px; box-shadow: 0 2px 8px rgba(0,0,0,0.3);
        z-index: 1000; display: flex; flex-direction: column; align-items: center;
        max-width: 90vw; max-height: 90vh;
    `;

    const title = document.createElement("h3");
    title.textContent = titleText;
    modal.appendChild(title);

    mediaElement.style.maxWidth = "100%";
    mediaElement.style.borderRadius = "8px";
    modal.appendChild(mediaElement);

    const btn = document.createElement("button");
    btn.style.marginTop = "10px";
    btn.textContent = isVideoRecording ? "Записать" : "Сделать фото";
    btn.id = "record-btn";
    btn.onclick = () => {
        onActionClick();
        if (!isVideoRecording) modal.remove();
    };
    modal.appendChild(btn);

    const closeBtn = document.createElement("button");
    closeBtn.textContent = "Закрыть";
    closeBtn.style.marginTop = "5px";
    closeBtn.onclick = () => {
        // Останавливаем камеры, если есть
        if (mediaElement.srcObject) {
            mediaElement.srcObject.getTracks().forEach(t => t.stop());
        }
        modal.remove();
    };
    modal.appendChild(closeBtn);

    return modal;
}

function createObservableFromEncryptedFile(file, aesKey, chunkSize = 64 * 1024) {
    return {
        subscribe: (observer)=> {
            let offset = 0;
            const fileSize = file.size;
            let cancelled = false;

            (async () => {
                try {
                    while (offset < fileSize && !cancelled) {
                        const chunk = file.slice(offset, offset + chunkSize);
                        const buffer = await chunk.arrayBuffer();
                        console.log("original", new Uint8Array(buffer));
                        const encryptedChunk = await encryptChunk(aesKey, buffer);
                        console.log("encryptedChunk", encryptedChunk);
                        observer.next(uint8ToBase64(encryptedChunk));
                        offset += chunkSize;
                    }

                    if (!cancelled) {
                        observer.complete();
                    }
                } catch (err) {
                    if (observer.error) {
                        observer.error(err);
                    } else {
                        console.error("Observable error:", err);
                    }
                }
            })();

            return {
                unsubscribe() {
                    cancelled = true;
                }
            };
        }
    };
}

function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

// Функция: создаём Observable, который читает файл по чанкам
function createFileObservable(file, chunkSize = 64 * 1024) {
    return {
        subscribe: (observer) => {
            const reader = new FileReader();
            let offset = 0;

            reader.onload = e => {
                if (e.target.error) {
                    observer.error(e.target.error);
                    return;
                }
                const chunk = new Uint8Array(e.target.result);
                observer.next(uint8ToBase64(chunk));
                offset += chunk.length;
                if (offset < file.size) {
                    readNextChunk();
                } else {
                    observer.complete();
                }
            };

            reader.onerror = e => observer.error(e.target.error);

            function readNextChunk() {
                const slice = file.slice(offset, offset + chunkSize);
                reader.readAsArrayBuffer(slice);
            }

            readNextChunk();

            // Возвращаем функцию отписки (если нужно)
            return {
                unsubscribe: () => {
                    // Тут можно отменить чтение, но FileReader не поддерживает отмену
                    // Можно поставить флаг, если хочешь.
                }
            };
        }
    };
}
