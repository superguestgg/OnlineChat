/**
 * Генерирует простое число заданной битности (упрощенная версия)
 * @param {number} bitLength - длина числа в битах (например, 32, 64)
 * @returns {bigint} - простое число
 */
function generatePrime(bitLength) {
    // В реальной реализации следует использовать криптографически безопасный
    // генератор простых чисел. Это упрощенный пример.
    const min = 1n << BigInt(bitLength - 1);
    const max = (1n << BigInt(bitLength)) - 1n;

    function isPrime(n) {
        if (n <= 1n) return false;
        if (n <= 3n) return true;

        if (n % 2n === 0n || n % 3n === 0n) return false;

        let i = 5n;
        while (i * i <= n) {
            if (n % i === 0n || n % (i + 2n) === 0n) return false;
            i += 6n;
        }
        return true;
    }

    let candidate;
    do {
        // Генерация случайного числа в диапазоне (не криптографически безопасно)
        const range = max - min;
        const randomBuffer = new Uint32Array(2);
        crypto.getRandomValues(randomBuffer);
        const randomValue = (BigInt(randomBuffer[0]) << 32n) | BigInt(randomBuffer[1]);
        candidate = min + (randomValue % range);

        // Делаем нечетным
        if (candidate % 2n === 0n) candidate += 1n;
    } while (!isPrime(candidate));

    return candidate;
}

/**
 * Находит первообразный корень (примитивный корень) по модулю p
 * @param {bigint} p - простое число
 * @returns {bigint} - первообразный корень по модулю p
 */
function findPrimitiveRoot(p) {
    // Факторизация p-1
    const factors = factorize(p - 1n);

    // Проверка кандидатов
    for (let g = 2n; g < p; g++) {
        let isRoot = true;
        for (const factor of factors) {
            const exponent = (p - 1n) / factor;
            if (modPow(g, exponent, p) === 1n) {
                isRoot = false;
                break;
            }
        }
        if (isRoot) return g;
    }
    return null;
}

/**
 * Факторизация числа (разложение на простые множители)
 * @param {bigint} n - число для факторизации
 * @returns {Set<bigint>} - множество уникальных простых множителей
 */
function factorize(n) {
    const factors = new Set();
    if (n === 1n) return factors;

    // Проверка на 2
    while (n % 2n === 0n) {
        factors.add(2n);
        n /= 2n;
    }

    // Проверка нечетных чисел до sqrt(n)
    let i = 3n;
    while (i * i <= n) {
        while (n % i === 0n) {
            factors.add(i);
            n /= i;
        }
        i += 2n;
    }

    if (n > 1n) factors.add(n);
    return factors;
}

/**
 * Возведение в степень по модулю (a^b mod m)
 * @param {bigint} a - основание
 * @param {bigint} b - показатель степени
 * @param {bigint} m - модуль
 * @returns {bigint} - результат a^b mod m
 */
function modPow(a, b, m) {
    let result = 1n;
    a = a % m;

    while (b > 0n) {
        if (b % 2n === 1n) {
            result = (result * a) % m;
        }
        a = (a * a) % m;
        b = b >> 1n;
    }

    return result;
}

/**
 * Генерирует секретный ключ
 * @param {bigint} p - простое число
 * @returns {bigint} - секретный ключ
 */
function generatePrivateKey(p) {
    // Генерируем случайное число от 2 до p-2
    const range = p - 2n;
    const randomBuffer = new Uint32Array(2);
    crypto.getRandomValues(randomBuffer);
    const randomValue = (BigInt(randomBuffer[0]) << 32n) | BigInt(randomBuffer[1]);
    return 2n + (randomValue % range);
}

/**
 * Генерирует публичный ключ
 * @param {bigint} g - первообразный корень
 * @param {bigint} privateKey - секретный ключ
 * @param {bigint} p - простое число
 * @returns {bigint} - публичный ключ
 */
function generatePublicKey(g, privateKey, p) {
    return modPow(g, privateKey, p);
}

/**
 * Вычисляет общий секретный ключ
 * @param {bigint} otherPublicKey - публичный ключ другой стороны
 * @param {bigint} privateKey - наш секретный ключ
 * @param {bigint} p - простое число
 * @returns {bigint} - общий секретный ключ
 */
function computeSharedSecret(otherPublicKey, privateKey, p) {
    return modPow(otherPublicKey, privateKey, p);
}

// Пример использования:
async function exampleUsage() {
    // 1. Генерация параметров (обычно делается один раз)
    const p = generatePrime(32); // В реальности нужно больше (2048 бит и более)
    const g = findPrimitiveRoot(p);

    console.log('Параметры:');
    console.log('p (простое число):', p.toString());
    console.log('g (первообразный корень):', g.toString());

    // 2. Алиса генерирует свои ключи
    const alicePrivateKey = generatePrivateKey(p);
    const alicePublicKey = generatePublicKey(g, alicePrivateKey, p);

    // 3. Боб генерирует свои ключи
    const bobPrivateKey = generatePrivateKey(p);
    const bobPublicKey = generatePublicKey(g, bobPrivateKey, p);

    console.log('\nКлючи Алисы:');
    console.log('Приватный:', alicePrivateKey.toString());
    console.log('Публичный:', alicePublicKey.toString());

    console.log('\nКлючи Боба:');
    console.log('Приватный:', bobPrivateKey.toString());
    console.log('Публичный:', bobPublicKey.toString());

    // 4. Обмен публичными ключами и вычисление общего секрета
    const aliceSharedSecret = computeSharedSecret(bobPublicKey, alicePrivateKey, p);
    const bobSharedSecret = computeSharedSecret(alicePublicKey, bobPrivateKey, p);

    console.log('\nОбщий секрет:');
    console.log('У Алисы:', aliceSharedSecret.toString());
    console.log('У Боба:', bobSharedSecret.toString());

    // Проверка, что ключи совпали
    console.log('\nКлючи совпадают:', aliceSharedSecret === bobSharedSecret);
}

// Запуск примера
exampleUsage().catch(console.error);