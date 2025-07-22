// ... (остальной JavaScript код остается без изменений до обработчиков событий)

// Генерация и отображение QR-кода
// ... (остальной ваш код)

// Генерация QR-кода
function generateQRCode() {
    const modal = document.getElementById('qr-modal');
    const canvas = document.getElementById('qr-canvas');
    const inviteLink = generateInviteLink(chatData.roomName);

    new QRious({
        element: canvas,
        value: inviteLink,
        size: 200,
        level: 'H' // Высокая устойчивость к ошибкам
    });

    modal.style.display = 'flex';
}

// Закрытие модального окна
function closeQRModal() {
    document.getElementById('qr-modal').style.display = 'none';
}

// Обработчики событий
document.getElementById('qr-btn').addEventListener('click', generateQRCode);
document.getElementById('qr-close').addEventListener('click', closeQRModal);

// Остальной JavaScript код остается без изменений