// Состояние приложения
const app = {
    connection: null,
    currentRoom: null,
    currentUser: null
};

// Инициализация приложения
document.addEventListener('DOMContentLoaded', async () => {
    initUI();
    await initConnection();
    checkUrlParams();
});

// Инициализация UI
function initUI() {
    // Переключение вкладок
    document.getElementById('join-tab').addEventListener('click', () => {
        document.getElementById('join-form').classList.remove('hidden');
        document.getElementById('create-form').classList.add('hidden');
        document.getElementById('join-tab').classList.add('active');
        document.getElementById('create-tab').classList.remove('active');
    });

    document.getElementById('create-tab').addEventListener('click', () => {
        document.getElementById('join-form').classList.add('hidden');
        document.getElementById('create-form').classList.remove('hidden');
        document.getElementById('join-tab').classList.remove('active');
        document.getElementById('create-tab').classList.add('active');
    });

    // Обработчики кнопок
    document.getElementById('join-btn').addEventListener('click', joinRoom);
    document.getElementById('create-btn').addEventListener('click', createRoom);
    document.getElementById('leave-btn').addEventListener('click', leaveRoom);
    document.getElementById('send-btn').addEventListener('click', sendMessage);
    document.getElementById('copy-btn').addEventListener('click', copyInviteLink);

    // Отправка по Enter
    document.getElementById('message-input').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendMessage();
    });
}

// Инициализация SignalR подключения
async function initConnection() {
    app.connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Регистрация обработчиков
    app.connection.on("ReceiveMessage", (user, message) => {
        addMessage(user, message, user === app.currentUser ? 'outgoing' : 'incoming');
    });

    app.connection.on("UserJoined", (user) => {
        addMessage('System', `${user} присоединился`, 'system');
    });

    app.connection.on("UserLeft", (user) => {
        addMessage('System', `${user} покинул чат`, 'system');
    });

    app.connection.on("UpdateUsers", (users) => {
        updateUsersList(users);
    });

    app.connection.on("RoomJoined", (roomInfo) => {
        showChatScreen(roomInfo);
    });

    try {
        await app.connection.start();
        console.log("SignalR подключен");
    } catch (err) {
        console.error("Ошибка подключения:", err);
    }
}

// Присоединение к комнате
async function joinRoom() {
    const roomId = document.getElementById('join-room-id').value.trim();
    const userName = document.getElementById('join-user-name').value.trim();

    if (!roomId || !userName) {
        alert("Заполните все поля");
        return;
    }

    showLoading(true);

    try {
        await app.connection.invoke("JoinRoom", roomId, userName);
    } catch (err) {
        console.error("Ошибка присоединения:", err);
        alert("Не удалось присоединиться: " + err.message);
        showLoading(false);
    }
}

// Создание комнаты
async function createRoom() {
    const roomName = document.getElementById('create-room-name').value.trim();
    const userName = document.getElementById('create-user-name').value.trim();

    if (!roomName || !userName) {
        alert("Заполните все поля");
        return;
    }

    showLoading(true);

    try {
        await app.connection.invoke("CreateRoom", roomName, userName);
    } catch (err) {
        console.error("Ошибка создания:", err);
        alert("Не удалось создать комнату: " + err.message);
        showLoading(false);
    }
}

// Покидание комнаты
async function leaveRoom() {
    try {
        await app.connection.invoke("LeaveRoom", app.currentRoom.id);
        app.currentRoom = null;
        app.currentUser = null;

        document.getElementById('chat-screen').classList.add('hidden');
        document.getElementById('start-screen').classList.remove('hidden');
        document.getElementById('messages').innerHTML = '';
    } catch (err) {
        console.error("Ошибка выхода:", err);
    }
}

// Отправка сообщения
async function sendMessage() {
    const message = document.getElementById('message-input').value.trim();
    if (!message) return;

    try {
        await app.connection.invoke("SendMessage", app.currentRoom.id, message);
        document.getElementById('message-input').value = '';
    } catch (err) {
        console.error("Ошибка отправки:", err);
    }
}

// Показать экран чата
function showChatScreen(roomInfo) {
    app.currentRoom = { id: roomInfo.roomId, name: roomInfo.roomName };
    app.currentUser = document.getElementById('join-user-name').value.trim() ||
        document.getElementById('create-user-name').value.trim();

    document.getElementById('start-screen').classList.add('hidden');
    document.getElementById('chat-screen').classList.remove('hidden');

    document.getElementById('room-name').textContent = roomInfo.roomName;

    const inviteLink = `${window.location.origin}?room=${roomInfo.roomId}&name=${encodeURIComponent(app.currentUser)}`;
    document.getElementById('invite-link').value = inviteLink;

    document.getElementById('message-input').disabled = false;
    document.getElementById('send-btn').disabled = false;
    document.getElementById('message-input').focus();

    showLoading(false);

    addMessage('System', `Вы присоединились к "${roomInfo.roomName}"`, 'system');
}

// Добавить сообщение в чат
function addMessage(user, text, type) {
    const msgElement = document.createElement('div');
    msgElement.className = `message ${type}`;
    msgElement.innerHTML = `<strong>${user}:</strong> ${text}`;
    document.getElementById('messages').appendChild(msgElement);
    document.getElementById('messages').scrollTop = document.getElementById('messages').scrollHeight;
}

// Обновить список пользователей
function updateUsersList(users) {
    const list = document.getElementById('users-list');
    list.innerHTML = '';

    users.forEach(user => {
        const userElement = document.createElement('div');
        userElement.textContent = user;
        if (user === app.currentUser) {
            userElement.style.fontWeight = 'bold';
        }
        list.appendChild(userElement);
    });

    document.getElementById('users-count').textContent = users.length;
}

// Копировать ссылку приглашения
function copyInviteLink() {
    const linkInput = document.getElementById('invite-link');
    linkInput.select();
    document.execCommand('copy');

    const copyBtn = document.getElementById('copy-btn');
    copyBtn.textContent = 'Скопировано!';
    setTimeout(() => {
        copyBtn.textContent = 'Копировать';
    }, 2000);
}

// Проверить URL параметры
function checkUrlParams() {
    const params = new URLSearchParams(window.location.search);
    const roomId = params.get('room');
    const userName = params.get('name');

    if (roomId && userName) {
        document.getElementById('join-room-id').value = roomId;
        document.getElementById('join-user-name').value = decodeURIComponent(userName);
        document.getElementById('join-tab').click();
        document.getElementById('join-btn').click();
    }
}

// Показать/скрыть индикатор загрузки
function showLoading(show) {
    document.getElementById('loading-overlay').classList.toggle('hidden', !show);
}