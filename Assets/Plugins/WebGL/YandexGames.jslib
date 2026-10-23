// Обёртка SDK Яндекс Игр (issue #328). Имена API — по документации:
// https://yandex.ru/dev/games/doc/ru/sdk/sdk-about (подключение и YaGames.init),
// .../sdk-game-events (LoadingAPI.ready, GameplayAPI.start/stop),
// .../sdk-events (game_api_pause / game_api_resume), .../sdk-environment (environment.i18n.lang),
// .../sdk-player (ysdk.getPlayer, player.getData/setData — облачные сохранения, issue #330).
// Вызывается из YandexJsBridge.cs, ответы идут в C# через SendMessage на объект receiver.
mergeInto(LibraryManager.library, {
  YG_Init: function (receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    var send = function (method, arg) {
      try { Module.SendMessage(receiver, method, arg || ''); }
      catch (e) { console.error('[YandexGames] SendMessage ' + method + ' failed', e); }
    };
    // Обещание SDK и игрока общие: сохранения ждут их, а не вызывают init второй раз (getPlayer: 20 запросов за 5 минут).
    window.__ygPlayer = function () {
      if (!window.__yandexPlayerPromise) {
        var sdkPromise = window.__yandexSdkPromise;
        var p = sdkPromise ? sdkPromise.then(function (s) { return s.getPlayer(); }) : Promise.reject(new Error('SDK не запущен'));
        p.catch(function () { window.__yandexPlayerPromise = null; });
        window.__yandexPlayerPromise = p;
      }
      return window.__yandexPlayerPromise;
    };
    var start = function () {
      window.__yandexSdkPromise = YaGames.init();
      window.__yandexSdkPromise.then(function (ysdk) {
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
    script.onerror = function () {
      window.__yandexSdkPromise = Promise.reject(new Error('sdk.js не загрузился'));
      window.__yandexSdkPromise.catch(function () {});
      send('OnSdkFailed', 'sdk.js не загрузился');
    };
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
  },

  // Облачные сохранения. Ответ уходит в C# на объект receiver (SaveController).
  YG_LoadSave: function (receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    if (!window.__ygPlayer) { Module.SendMessage(receiver, 'OnCloudLoadFailed', 'YG_Init не вызывался'); return; }
    window.__ygPlayer()
      .then(function (player) { return player.getData(['save']); })
      .then(function (data) { Module.SendMessage(receiver, 'OnCloudLoaded', (data && data.save) || ''); })
      .catch(function (e) { Module.SendMessage(receiver, 'OnCloudLoadFailed', String(e)); });
  },

  YG_WriteSave: function (receiverPtr, jsonPtr) {
    var receiver = UTF8ToString(receiverPtr);
    var json = UTF8ToString(jsonPtr);
    if (!window.__ygPlayer) { Module.SendMessage(receiver, 'OnCloudWritten', '0'); return; }
    window.__ygPlayer()
      .then(function (player) { return player.setData({ save: json }, true); })
      .then(function () { Module.SendMessage(receiver, 'OnCloudWritten', '1'); })
      .catch(function (e) { console.error('[YandexGames] setData failed', e); Module.SendMessage(receiver, 'OnCloudWritten', '0'); });
  }
});
