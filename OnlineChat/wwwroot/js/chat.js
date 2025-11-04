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

// Функция для изменения имени пользователя
async function changeUsername() {
    const newUsername = editUsernameInput.value.trim();
    if (!newUsername) return;

    try {
        const result = await connection.invoke("ChangeUserName", chatData.roomId, newUsername);
        console.log(result);
        if (result.item1) {
            chatData.username = newUsername;
            console.log("Имя успешно изменено");
        } else {
            alert("Ошибка: " + result.item2);
        }
    } catch (err) {
        console.error("Ошибка при изменении имени пользователя:", err);
        alert("Не удалось изменить имя пользователя");
    }
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

// Мобильное меню участников (sidebar)
const sidebarToggleBtn = document.getElementById('sidebar-toggle-btn');
const sidebarWrapper = document.getElementById('sidebar-wrapper');
const sidebarOverlay = document.getElementById('sidebar-overlay');
const sidebarCloseBtn = document.getElementById('sidebar-close-btn');

function isMobile() {
    return window.matchMedia('(max-width: 600px)').matches;
}

function openSidebar() {
    sidebarWrapper.style.transform = 'translateX(0)';
    sidebarWrapper.style.boxShadow = '2px 0 10px rgba(0,0,0,0.2)';
    sidebarOverlay.style.display = 'block';
}

function closeSidebar() {
    sidebarWrapper.style.transform = isMobile() ? 'translateX(-100%)' : 'none';
    sidebarOverlay.style.display = 'none';
}

function updateSidebarToggleVisibility() {
    if (isMobile()) {
        sidebarToggleBtn.style.display = 'inline-block';
        if (sidebarCloseBtn) sidebarCloseBtn.style.display = 'inline-block';
        closeSidebar();
    } else {
        sidebarToggleBtn.style.display = 'none';
        if (sidebarCloseBtn) sidebarCloseBtn.style.display = 'none';
        sidebarWrapper.style.transform = 'none';
        sidebarOverlay.style.display = 'none';
    }
}

sidebarToggleBtn.addEventListener('click', function() {
    if (sidebarWrapper.style.transform === 'translateX(0px)' || sidebarWrapper.style.transform === 'translateX(0%)') {
        closeSidebar();
    } else {
        openSidebar();
    }
});

sidebarOverlay.addEventListener('click', closeSidebar);

if (sidebarCloseBtn) {
    sidebarCloseBtn.addEventListener('click', closeSidebar);
}

window.addEventListener('resize', updateSidebarToggleVisibility);
document.addEventListener('DOMContentLoaded', updateSidebarToggleVisibility);