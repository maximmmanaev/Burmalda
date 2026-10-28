namespace Burmalda.FateRoll
{
    /// <summary>Итог одного Броска Судьбы — для UI и аналитики (PRD v10 §30: индекс, кубики, бонус, сумма, исход).</summary>
    public readonly struct FateRollResult
    {
        public FateRollResult(int rollIndex, int die1, int die2, int modifier, FateOutcome outcome)
        {
            RollIndex = rollIndex;
            Die1 = die1;
            Die2 = die2;
            Modifier = modifier;
            Outcome = outcome;
        }

        public int RollIndex { get; }
        public int Die1 { get; }
        public int Die2 { get; }
        public int Modifier { get; }
        public int Sum => Die1 + Die2 + Modifier;
        public FateOutcome Outcome { get; }
    }
}
