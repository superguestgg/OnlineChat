function getRandomNumber(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function getUrlParams() {
    const urlParams = new URLSearchParams(window.location.search);
    return {
        roomId: urlParams.get('room'),
        name: urlParams.get('name') ? decodeURIComponent(urlParams.get('name')) : null
    };
}

// Добавление сообщения в чат
function addMessage(user, text, type, findLinks=true) {
    const messageElement = document.createElement('div');
    messageElement.className = `message ${type}`;
    
    // Функция для замены URL на ссылки
    const linkifyText = (inputText) => {
        // Регулярное выражение для поиска URL
        const urlRegex = /(https?:\/\/[^\s]+)/g;
        return inputText.replace(urlRegex, (url) => {
            // Обрезаем возможные trailing символы (точки, запятые и т.д.)
            const cleanUrl = url.replace(/[.,;!?]+$/, '');
            return `<a href="${cleanUrl}" target="_blank" rel="noopener noreferrer">${cleanUrl}</a>`;
        });
    };
    
    if (findLinks){
        text = linkifyText(text);
    }


    messageElement.innerHTML = `<strong>${user}:</strong> ${text}`;

    messagesContainer.appendChild(messageElement);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

// Генерация ссылки для приглашения
function generateInviteLink(roomName) {
    const currentUrl = new URL(window.location.href);
    currentUrl.searchParams.set('room', chatData.roomId);
    currentUrl.searchParams.set('name', encodeURIComponent(roomName));
    return currentUrl.toString();
}

// Копирование ссылки
function copyInviteLink() {
    inviteLinkInput.select();
    document.execCommand('copy');

    // Показываем подтверждение
    copySuccess.style.display = 'block';
    setTimeout(() => {
        copySuccess.style.display = 'none';
    }, 2000);
}

// Обновление списка пользователей
function updateUsersList(users) {
    usersList.innerHTML = '';
    users.forEach(user => {
        const userItem = document.createElement('li');
        userItem.className = 'user-item';
        userItem.innerHTML = `
                    <span class="user-status"></span>
                    ${user} ${user === chatData.username ? '(Вы)' : ''}
                `;
        usersList.appendChild(userItem);
    });
    usersCount.textContent = users.length;
}

// Покидание комнаты
async function leaveRoom() {
    try {
        await connection.invoke("LeaveRoom", chatData.roomId);
        window.close();
    } catch (err) {
        console.error("Error leaving room:", err);
    }
}