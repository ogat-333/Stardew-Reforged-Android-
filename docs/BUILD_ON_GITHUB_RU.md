# Сборка Stardew Reforged Android через GitHub (без компьютера)

Сборка запускается на серверах GitHub Actions. Компьютер или ноутбук не нужны.

## Запустить сборку

1. Откройте вкладку Actions репозитория:
   https://github.com/ogat-333/Stardew-Reforged-Android-/actions
2. Выберите **Build Reforged Android launcher**.
3. Нажмите **Run workflow**.
4. В поле **Direct Dropbox download URL** оставьте предложенную ссылку или вставьте другую прямую ссылку на проверенный APK.
5. Нажмите **Run workflow** ещё раз и дождитесь завершения.
6. При успешной сборке откройте запуск и скачайте артефакт **Stardew-Reforged-Android-launcher**.

Ссылка по умолчанию ведёт на проверенный APK:
- размер: 430 623 152 байта;
- SHA-256: `b9ae4727e10a89b144a1301076ebb1e38586aba13f85c4c86ad9d5c3aaf21a28`.

Если ссылка требует Bearer-токен, добавьте его в настройках репозитория как secret `GAME_APK_BEARER_TOKEN`:
https://github.com/ogat-333/Stardew-Reforged-Android-/settings/secrets/actions

## Что делает workflow

- скачивает APK и сверяет SHA-256;
- извлекает только три DLL, нужные для компиляции;
- хранит APK и DLL только во временной папке сервера сборки;
- не коммитит APK или игровые DLL в репозиторий и не включает их в артефакт;
- собирает Android ARM64 launcher APK.

Это сборка загрузчика, а не подтверждение запуска игры с модами. После сборки потребуется установить загрузчик и проверить его на Android.
