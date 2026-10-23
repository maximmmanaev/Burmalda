using System;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Основное хранилище (облако) + запасное (локальное). Запись идёт в оба. Чтение берёт новейшую
    /// читаемую копию по <see cref="SaveReadResult.SavedAt"/> (при равенстве — основную); если читаемых нет,
    /// возвращает «более новая версия», затем повреждённый текст — чтобы <see cref="SaveCoordinator"/> видел причину.
    /// </summary>
    public sealed class CompositeSaveStorage : ISaveStorage
    {
        private readonly ISaveStorage _primary;
        private readonly ISaveStorage _secondary;

        public CompositeSaveStorage(ISaveStorage primary, ISaveStorage secondary)
        {
            _primary = primary;
            _secondary = secondary;
        }

        public void Read(Action<string> onRead)
        {
            _primary.Read(fromPrimary => _secondary.Read(fromSecondary => onRead(Pick(fromPrimary, fromSecondary))));
        }

        public void Write(string text, Action<bool> onDone)
        {
            var pending = 2;
            var anyOk = false;
            Action<bool> one = ok =>
            {
                anyOk |= ok;
                if (--pending == 0) onDone(anyOk);
            };
            _secondary.Write(text, one);
            _primary.Write(text, one);
        }

        private static string Pick(string a, string b)
        {
            var ra = SaveFormat.Deserialize(a);
            var rb = SaveFormat.Deserialize(b);

            if (ra.Status == SaveReadStatus.Ok && rb.Status == SaveReadStatus.Ok) return rb.SavedAt > ra.SavedAt ? b : a;
            if (ra.Status == SaveReadStatus.Ok) return a;
            if (rb.Status == SaveReadStatus.Ok) return b;
            if (ra.Status == SaveReadStatus.TooNew) return a;
            if (rb.Status == SaveReadStatus.TooNew) return b;
            if (ra.Status == SaveReadStatus.Corrupt) return a;
            if (rb.Status == SaveReadStatus.Corrupt) return b;
            return null;
        }
    }
}
