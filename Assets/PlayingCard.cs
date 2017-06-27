using System.Linq;
using Rnd = UnityEngine.Random;

namespace PointOfOrder
{
    enum Suit { Spades, Hearts, Clubs, Diamonds }
    enum Rank { Ace, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }

    struct PlayingCard
    {
        private int _card;
        public Suit Suit { get { return (Suit) (_card % 4); } }
        public Rank Rank { get { return (Rank) (_card / 4); } }
        public PlayingCard(int card) { _card = card; }
        public static PlayingCard GetRandom() { return new PlayingCard(Rnd.Range(0, 13 * 4)); }
        public static PlayingCard[] AllCards = Enumerable.Range(0, 13 * 4).Select(i => new PlayingCard(i)).ToArray();
        private static string[] _rankNames = "A|2|3|4|5|6|7|8|9|10|J|Q|K".Split('|');
        public override string ToString() { return _rankNames[(int) Rank] + "" + "♠♥♣♦"[(int) Suit]; }
    }
}
