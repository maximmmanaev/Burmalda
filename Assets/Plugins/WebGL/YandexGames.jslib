// Обёртка SDK Яндекс Игр (issue #328). Имена API — по документации:
// https://yandex.ru/dev/games/doc/ru/sdk/sdk-about (подключение и YaGames.init),
// .../sdk-game-events (LoadingAPI.ready, GameplayAPI.start/stop),
// .../sdk-events (game_api_pause / game_api_resume), .../sdk-environment (environment.i18n.lang).
// Вызывается из YandexJsBridge.cs, ответы идут в C# через SendMessage на объект receiver.
mergeInto(LibraryManager.library, {
  YG_Init: function (receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    var send = function (method, arg) {
      try { Module.SendMessage(receiver, method, arg || ''); }
      catch (e) { console.error('[YandexGames] SendMessage ' + method + ' failed', e); }
    };
    var start = function () {
      YaGames.init().then(function (ysdk) {
        window.__yandexSdk = ysdk;
        ysdk.on('game_api_pause', function () { send('OnPause'); });
        ysdk.on('game_api_resume', function () { send('OnResume'); });
        var lang = '';
        try { lang = ysdk.environment.i18n.lang || ''; } catch (e) { console.warn('[YandexGames] язык недоступен', e); }
        send('OnSdkInitialized', lang);
      }).catch(function (e) {
        console.error('[YandexGames] init failed', e);
        send('OnSdkFailed', String(e));
      });
    };
    if (typeof YaGames !== 'undefined') { start(); return; }
    var script = document.createElement('script');
    script.src = '/sdk.js'; // относительный путь — для архива, загружаемого через Консоль разработчика
    script.async = true;
    script.onload = start;
    script.onerror = function () { send('OnSdkFailed', 'sdk.js не загрузился'); };
    document.head.appendChild(script);
  },

  YG_Ready: function () {
    var sdk = window.__yandexSdk;
    if (sdk && sdk.features && sdk.features.LoadingAPI) sdk.features.LoadingAPI.ready();
  },

  YG_GameplayStart: function () {
    var sdk = window.__yandexSdk;
    if (sdk && sdk.features && sdk.features.GameplayAPI) sdk.features.GameplayAPI.start();
  },

  YG_GameplayStop: function () {
    var sdk = window.__yandexSdk;
    if (sdk && sdk.features && sdk.features.GameplayAPI) sdk.features.GameplayAPI.stop();
  }
});
