using System;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Хранилище текста сохранения. Операции асинхронные (облако SDK отвечает позже); колбэк вызывается
    /// ровно один раз. Чтение: null — данных нет или сбой. Запись: false — не удалось.
    /// </summary>
    public interface ISaveStorage
    {
        void Read(Action<string> onRead);
        void Write(string text, Action<bool> onDone);
    }
}
