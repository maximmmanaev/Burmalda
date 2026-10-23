using System;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Оболочка сохранения для <c>JsonUtility</c>: версия формата, время записи (Unix, секунды) и
    /// <see cref="SaveData"/> строкой JSON — по строке видно, что данных нет, а <c>JsonUtility</c> не
    /// подставляет пустой объект вместо отсутствующего.
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        public int version;
        public long savedAt;
        public string payload;
    }
}
