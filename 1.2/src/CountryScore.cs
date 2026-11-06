namespace PowerRanking
{
    // One ranking row: the kingdom reference, raw metrics, six sub-scores and
    // the final composite score computed by PowerRankingEngine.
    public class CountryScore
    {
        public Kingdom kingdom;
        public string name;
        public float score;

        public float subMilitary;
        public float subEconomy;
        public float subPopulation;
        public float subTerritory;
        public float subDevelopment;
        public float subInfluence;

        public int military;
        public long economy;
        public int population;
        public int territory;
        public int development;
        public int influence;
    }
}
