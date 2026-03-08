// Отрисовка комнат
function renderRooms(rooms) {
    roomsContainer.innerHTML = '';

    if (rooms.length === 0) {
        roomsContainer.innerHTML = '<p>Нет доступных комнат</p>';
        return;
    }

    rooms.forEach(room => {
        const roomCard = document.createElement('div');
        roomCard.className = 'room-card';
        roomCard.innerHTML = `
                <h3>${room.name}</h3>
                <p>ID: ${room.id}</p>
                <div class="room-meta">
                    <span>Участников: <span class="user-count">${room.userCount}</span></span>
                </div>
            `;

        roomCard.addEventListener('click', () => {
            const username = usernameInput.value.trim();
            if (!username) {
                alert("Пожалуйста, введите ваше имя");
                return;
            }

            // Сохраняем данные и открываем чат
            sessionStorage.setItem('chatData', JSON.stringify({
                roomId: room.id,
                username: username,
                roomName: room.name
            }));

            window.open('chat.html', '_blank');
        });

        roomsContainer.appendChild(roomCard);
    });
}

// Обновление таймера
function updateTimer() {
    const now = Date.now();
    const timePassed = now - lastRefreshTime;
    const timeLeft = Math.max(0, refreshCooldown - timePassed);

    if (timeLeft > 0) {
        refreshBtn.disabled = true;
        refreshTimer.textContent = `(${Math.ceil(timeLeft / 1000)}s)`;
    } else {
        refreshBtn.disabled = false;
        refreshTimer.textContent = '';
        clearInterval(timerInterval);
    }
}

// Обновление списка комнат
async function refreshRooms() {
    const now = Date.now();
    if (now - lastRefreshTime < refreshCooldown) {
        return;
    }

    lastRefreshTime = now;
    refreshBtn.disabled = true;

    // Запускаем таймер
    clearInterval(timerInterval);
    timerInterval = setInterval(updateTimer, 200);
    updateTimer();

    try {
        await loadRooms();
    } catch (err) {
        console.error("Ошибка обновления:", err);
    }
}


// Загрузка комнат
async function loadRooms() {
    try {
        loadingIndicator.style.display = 'block';
        roomsContainer.style.opacity = '0.5';

        const rooms = await connection.invoke("GetAllRooms");
        renderRooms(rooms);
    } catch (err) {
        console.error("Ошибка загрузки комнат:", err);
        alert("Ошибка при загрузке списка комнат");
    } finally {
        loadingIndicator.style.display = 'none';
        roomsContainer.style.opacity = '1';
    }
}

// Запуск подключения и первоначальная загрузка
async function start() {
    try {
        await connection.start();
        console.log("Connected to RoomHub");
        await loadRooms();
    } catch (err) {
        console.error("Connection error:", err);
        setTimeout(start, 5000);
    }
}

function generateRandomUsername(){

    const adjectives = [
        "Crazy",
        "Sleepy",
        "Angry",
        "Happy",
        "Sneaky",
        "Fluffy",
        "Wild",
        "Dancing",
        "Turbo",
        "Lazy",
        "Shiny",
        "Epic"
    ];

    const animals = [
        "Fox",
        "Cat",
        "Wolf",
        "Tiger",
        "Bear",
        "Panda",
        "Otter",
        "Hawk",
        "Raccoon",
        "Lizard",
        "Penguin",
        "Hamster"
    ];

    const adj = adjectives[Math.floor(Math.random() * adjectives.length)];
    const animal = animals[Math.floor(Math.random() * animals.length)];
    const number = Math.floor(100 + Math.random() * 900);

    return `${adj}${animal}${number}`;
}

function initUsername(){
    let savedName = localStorage.getItem("chatUsername");

    if(!savedName){
        savedName = generateRandomUsername();
        localStorage.setItem("chatUsername", savedName);
    }

    usernameInput.value = savedName;
}
    