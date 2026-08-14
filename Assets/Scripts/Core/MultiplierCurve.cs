namespace Burmalda.Core
{
    /// <summary>
    /// Кривая множителя добычи (PRD 4.3): множитель растёт по кривой в
    /// зависимости от "эффективной длины" трейла — числа уникальных
    /// собранных плит с бонусом за не-прямолинейные пути (см.
    /// <c>Burmalda.Movement.TrailMultiplierSystem</c>, который считает саму
    /// эффективную длину). Эта часть — чистая функция, буквально перенесена
    /// из legacy/burmolda_demo.html (MCURVE/getM), как и пороги Decay/
    /// TunnelObstacleGenerator.
    /// </summary>
    public static class MultiplierCurve
    {
        /// <summary>Множитель после исчерпания кривой (см. getM: "n>=MCURVE.length?20:...").</summary>
        public const int MaxMultiplier = 20;

        // Буквально из legacy/burmolda_demo.html:
        // const MCURVE=[1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,4,4,4,4,4,4,4,4,7,7,7,7,7,7,7,12,12,12,12,12,20];
        private static readonly int[] Curve =
        {
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2,
            4, 4, 4, 4, 4, 4, 4, 4,
            7, 7, 7, 7, 7, 7, 7,
            12, 12, 12, 12, 12,
            20
        };

        /// <summary>
        /// Множитель для заданной эффективной длины трейла. За пределами
        /// кривой (включая длину, равную её размеру) — <see cref="MaxMultiplier"/>
        /// (getM: "n>=MCURVE.length?20:(MCURVE[n]||1)"). Отрицательная длина
        /// (не должна происходить в норме) клампится к 0 — защитно, не как
        /// требование PRD.
        /// </summary>
        public static int GetMultiplier(int effectiveLength)
        {
            if (effectiveLength < 0) effectiveLength = 0;
            return effectiveLength >= Curve.Length ? MaxMultiplier : Curve[effectiveLength];
        }
    }
}
