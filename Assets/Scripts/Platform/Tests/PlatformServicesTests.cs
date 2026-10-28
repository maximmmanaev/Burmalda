using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Platform.Tests
{
    /// <summary>Вызовы моста SDK в порядке поступления — вместо реального .jslib.</summary>
    internal sealed class FakeYandexBridge : IYandexBridge
    {
        public readonly List<string> Calls = new List<string>();
        public void Init() => Calls.Add("init");
        public void Ready() => Calls.Add("ready");
        public void GameplayStart() => Calls.Add("start");
        public void GameplayStop() => Calls.Add("stop");
    }

    internal sealed class FakePlatformServices : IPlatformServices
    {
        public readonly List<string> Calls = new List<string>();
        public bool IsInitialized => true;
        public string Language => "ru";
#pragma warning disable CS0067
        public event System.Action Initialized;
#pragma warning restore CS0067
        public event System.Action Paused;
        public event System.Action Resumed;
        public void Initialize() => Calls.Add("init");
        public void SignalReady() => Calls.Add("ready");
        public void GameplayStart() => Calls.Add("start");
        public void GameplayStop() => Calls.Add("stop");
        public void RaisePaused() => Paused?.Invoke();
        public void RaiseResumed() => Resumed?.Invoke();
    }

    public class PlatformServicesFactoryTests
    {
        [Test]
        public void Select_NotWebGlPlayer_ReturnsNull()
        {
            Assert.IsInstanceOf<NullPlatformServices>(PlatformServicesFactory.Select(false));
        }

        [Test]
        public void Select_WebGlPlayer_ReturnsYandex()
        {
            Assert.IsInstanceOf<YandexPlatformServices>(PlatformServicesFactory.Select(true));
        }

        [Test]
        public void Create_InEditor_ReturnsNull()
        {
            Assert.IsInstanceOf<NullPlatformServices>(PlatformServicesFactory.Create());
        }
    }

    public class NullPlatformServicesTests
    {
        [Test]
        public void Initialize_RaisesInitializedAndSetsFlag()
        {
            var s = new NullPlatformServices();
            var raised = 0;
            s.Initialized += () => raised++;
            Assert.IsFalse(s.IsInitialized);
            s.Initialize();
            Assert.IsTrue(s.IsInitialized);
            Assert.AreEqual(1, raised);
            Assert.IsNull(s.Language);
        }

        [Test]
        public void OtherCalls_DoNotThrow()
        {
            var s = new NullPlatformServices();
            Assert.DoesNotThrow(() => { s.SignalReady(); s.GameplayStart(); s.GameplayStop(); });
        }
    }

    public class YandexPlatformServicesTests
    {
        private FakeYandexBridge _bridge;
        private YandexPlatformServices _s;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeYandexBridge();
            _s = new YandexPlatformServices(_bridge);
        }

        [Test]
        public void Initialize_CallsBridgeOnce()
        {
            _s.Initialize();
            _s.Initialize();
            CollectionAssert.AreEqual(new[] { "init" }, _bridge.Calls);
        }

        [Test]
        public void CallsBeforeSdkReady_AreHeldUntilInitialized_ReadyFirstThenStart()
        {
            _s.Initialize();
            _s.GameplayStart();
            _s.SignalReady();
            CollectionAssert.AreEqual(new[] { "init" }, _bridge.Calls);

            _s.OnSdkInitialized("ru");

            CollectionAssert.AreEqual(new[] { "init", "ready", "start" }, _bridge.Calls);
            Assert.IsTrue(_s.IsInitialized);
            Assert.AreEqual("ru", _s.Language);
        }

        [Test]
        public void StartThenStopBeforeInit_SendsNothing()
        {
            _s.Initialize();
            _s.GameplayStart();
            _s.GameplayStop();
            _s.OnSdkInitialized("en");
            CollectionAssert.AreEqual(new[] { "init" }, _bridge.Calls);
        }

        [Test]
        public void AfterInit_CallsGoStraightThrough_AndAreNotDuplicated()
        {
            _s.Initialize();
            _s.OnSdkInitialized("en");
            _s.SignalReady();
            _s.SignalReady();
            _s.GameplayStart();
            _s.GameplayStart();
            _s.GameplayStop();
            _s.GameplayStop();
            CollectionAssert.AreEqual(new[] { "init", "ready", "start", "stop" }, _bridge.Calls);
        }

        [Test]
        public void SdkFailed_NotInitialized_CallsNeverReachBridge()
        {
            var raised = 0;
            _s.Initialized += () => raised++;
            _s.Initialize();
            _s.SignalReady();
            _s.OnSdkFailed();
            _s.SignalReady();
            _s.GameplayStart();
            CollectionAssert.AreEqual(new[] { "init" }, _bridge.Calls);
            Assert.IsFalse(_s.IsInitialized);
            Assert.IsNull(_s.Language);
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void PauseAndResume_AreForwardedAsEvents_WithoutGameplayCalls()
        {
            _s.Initialize();
            _s.OnSdkInitialized("ru");
            _bridge.Calls.Clear();
            var log = new List<string>();
            _s.Paused += () => log.Add("p");
            _s.Resumed += () => log.Add("r");
            _s.OnPause();
            _s.OnResume();
            CollectionAssert.AreEqual(new[] { "p", "r" }, log);
            // платформа сама вызывает stop/start на паузе (docs: game_api_pause/resume)
            CollectionAssert.IsEmpty(_bridge.Calls);
        }
    }

    public class GameplayMarksTests
    {
        [Test]
        public void RunStartedThenEnded_CallsStartThenStop()
        {
            var fake = new FakePlatformServices();
            var marks = new GameplayMarks(fake);
            marks.RunStarted();
            marks.RunEnded();
            CollectionAssert.AreEqual(new[] { "start", "stop" }, fake.Calls);
        }

        [Test]
        public void Repeats_AreIgnored_AndRestartAfterEndWorks()
        {
            var fake = new FakePlatformServices();
            var marks = new GameplayMarks(fake);
            marks.RunEnded();
            marks.RunStarted();
            marks.RunStarted();
            marks.RunEnded();
            marks.RunEnded();
            marks.RunStarted();
            CollectionAssert.AreEqual(new[] { "start", "stop", "start" }, fake.Calls);
        }
    }

    public class PlatformPauseControllerTests
    {
        [Test]
        public void PauseThenResume_AppliesTrueThenFalse()
        {
            var fake = new FakePlatformServices();
            var applied = new List<bool>();
            using (new PlatformPauseController(fake, applied.Add))
            {
                fake.RaisePaused();
                fake.RaiseResumed();
            }
            CollectionAssert.AreEqual(new[] { true, false }, applied);
        }

        [Test]
        public void DoubleResume_AppliesOnce_DoublePause_AppliesOnce()
        {
            var fake = new FakePlatformServices();
            var applied = new List<bool>();
            using (new PlatformPauseController(fake, applied.Add))
            {
                fake.RaisePaused();
                fake.RaisePaused();
                fake.RaiseResumed();
                fake.RaiseResumed();
            }
            CollectionAssert.AreEqual(new[] { true, false }, applied);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var fake = new FakePlatformServices();
            var applied = new List<bool>();
            new PlatformPauseController(fake, applied.Add).Dispose();
            fake.RaisePaused();
            CollectionAssert.IsEmpty(applied);
        }

        [Test]
        public void DefaultApplier_SetsTimeScaleAndAudio_AndRestoresPreviousTimeScale()
        {
            var fake = new FakePlatformServices();
            var prevScale = Time.timeScale;
            var prevAudio = AudioListener.pause;
            try
            {
                Time.timeScale = 0.5f;
                using (new PlatformPauseController(fake))
                {
                    fake.RaisePaused();
                    Assert.AreEqual(0f, Time.timeScale);
                    Assert.IsTrue(AudioListener.pause);
                    fake.RaiseResumed();
                    Assert.AreEqual(0.5f, Time.timeScale);
                    Assert.IsFalse(AudioListener.pause);
                }
            }
            finally
            {
                Time.timeScale = prevScale;
                AudioListener.pause = prevAudio;
            }
        }
    }
}
