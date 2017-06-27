using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PointOfOrder;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Point of Order
/// Created by Timwi
/// </summary>
public class PointOfOrderModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public GameObject CardObj;
    public Transform Main;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private const int _numActiveRules = 2;
    private const int _numPlayedCards = 5;
    private const int _minWrongCards = 4;
    private const int _numChoiceCards = 4;

    enum State { FaceDown, FaceUp, Transitioning, WrongCardPressed, Solved }
    private State _state;

    private KMSelectable[] _choiceCards;
    private Transform[] _choiceCardCards;
    private Transform[] _choiceCardHighlights;
    private MeshRenderer[] _frontFaces;
    private MeshRenderer[] _backFaces;
    private bool[] _choiceCardLocked = new bool[_numChoiceCards];

    private int _correctCardIndex;
    private List<PlayingCard> _pile;
    private PlayingCard[] _acceptableCards;
    private PlayingCard[] _possibleWrongCards;
    private Func<PlayingCard, List<PlayingCard>, bool>[] _activeRules;
    private Func<PlayingCard, List<PlayingCard>, bool>[] _inactiveRules;

    private const bool _fullLogging = false;

    public void Start()
    {
        _moduleId = _moduleIdCounter++;

        // Delay preparation so that edgework is available.
        StartCoroutine(prepare());
    }

    IEnumerator prepare()
    {
        _state = State.FaceDown;

        yield return new WaitForSeconds(.1f);

        Debug.LogFormat("[Point of Order #{0}] Point of order.", _moduleId);

        var serial = Bomb.GetSerialNumber();
        var serial1Letter = char.IsLetter(serial[0]);
        var serial2Letter = char.IsLetter(serial[1]);
        var divisibleBy = (serial[3] - 'A' + 1) % 3 + 3;
        var difference = (serial[4] - 'A' + 1) % 3 + 2;
        var allowedSuits = (serial1Letter
                ? serial2Letter ? "01;12;23;30" : "03;10;21;32"
                : serial2Letter ? "12;23;30;01" : "32;03;10;21").Split(';');

        if (_fullLogging)
        {
            Debug.LogFormat("[Point of Order #{0}] Rule 1: Allowed suits: {1}", _moduleId, allowedSuits.Select((s, i) => "♠♥♣♦"[i] + " → " + s.Select(ch => "♠♥♣♦"[ch - '0'].ToString()).JoinString("/")).JoinString("; "));
            Debug.LogFormat("[Point of Order #{0}] Rule 2: Ranks must alternate between being divisible by {1} and not.", _moduleId, divisibleBy);
            Debug.LogFormat("[Point of Order #{0}] Rule 3: Consecutive ranks must have a difference between {1} and {2} (with wraparound allowed).", _moduleId, difference, difference + 1);

            // OLD
            //Debug.LogFormat("[Point of Order #{0}] Rule 1: Ranks must {1}.", _moduleId, lit < unlit ? "go asc, asc, desc" : lit > unlit ? "go desc, desc, asc" : "alternate between descending and ascending");
            //Debug.LogFormat(serial1Letter
            //    ? serial2Letter
            //        ? "[Point of Order #{0}] Rule 2: No two consecutive cards of same suit."
            //        : "[Point of Order #{0}] Rule 2: Can’t have ♠ touch ♣ or ♥ touch ♦."
            //    : serial2Letter
            //        ? "[Point of Order #{0}] Rule 2: Can’t have ♠ touch ♥ or ♣ touch ♦."
            //        : "[Point of Order #{0}] Rule 2: Can’t have ♠ touch ♦ or ♣ touch ♥.", _moduleId);
            //Debug.LogFormat("[Point of Order #{0}] Rule 3: Ranks must alternate between being divisible by {1} and not.", _moduleId, divisibleBy);
            //Debug.LogFormat("[Point of Order #{0}] Rule 4: Consecutive ranks must have a difference between {1} and {2} (with wraparound allowed).", _moduleId, difference, difference + 3);
            //Debug.LogFormat("[Point of Order #{0}] Rule 5: {1} OR {2} rank difference", _moduleId,
            //    suitAssocNum == 0 ? "Same suit" : string.Format("Associated suit (♠↔{0}, {1}↔{2})", "♣♥♦"[suitAssocNum - 1], "♥♣♣"[suitAssocNum - 1], "♦♦♥"[suitAssocNum - 1]),
            //    numAABatteries > numDBatteries ? "odd" : numAABatteries < numDBatteries ? "even" : "prime");
        }

        var rules = Ut.NewArray<Func<PlayingCard, List<PlayingCard>, bool>>(
            //// Rule 1: Ranks must be 2×ascending+1×descending / 2×descending+1×ascending / alternate between descending and ascending
            //(card, cards) =>
            //{
            //    if (card.Rank == cards.Last().Rank)
            //        return false;
            //    if (cards.Count < 2)
            //        return true;

            //    if (lit < unlit)    // 2×asc, 1×desc
            //    {
            //        if (cards[1].Rank < cards[0].Rank)
            //            return (card.Rank > cards.Last().Rank) ^ (cards.Count % 3 == 1);
            //        if (cards.Count < 3)
            //            return true;
            //        if (cards[2].Rank < cards[1].Rank)
            //            return (card.Rank > cards.Last().Rank) ^ (cards.Count % 3 == 2);
            //        return (card.Rank > cards.Last().Rank) ^ (cards.Count % 3 == 0);
            //    }

            //    if (lit > unlit)     // 2×desc, 1×asc
            //    {
            //        if (cards[1].Rank > cards[0].Rank)
            //            return (card.Rank < cards.Last().Rank) ^ (cards.Count % 3 == 1);
            //        if (cards.Count < 3)
            //            return true;
            //        if (cards[2].Rank > cards[1].Rank)
            //            return (card.Rank < cards.Last().Rank) ^ (cards.Count % 3 == 2);
            //        return (card.Rank < cards.Last().Rank) ^ (cards.Count % 3 == 0);
            //    }

            //    // alternate between desc and asc
            //    return card.Rank != cards.Last().Rank && ((card.Rank > cards.Last().Rank) ^ (cards[1].Rank > cards[0].Rank) ^ (cards.Count % 2 != 0));
            //},

            // OUTDATED Rule 2: No two consecutive cards of associated suits
            (card, cards) => allowedSuits[(int) cards.Last().Suit].Contains((char) ('0' + (int) card.Suit)),
            //(card, cards) => serial1Letter
            //    ? serial2Letter
            //        ? card.Suit != cards.Last().Suit
            //        : card.Suit == Suit.Spades ? cards.Last().Suit != Suit.Clubs : card.Suit == Suit.Clubs ? cards.Last().Suit != Suit.Spades : card.Suit == Suit.Diamonds ? cards.Last().Suit != Suit.Hearts : cards.Last().Suit != Suit.Diamonds
            //    : serial2Letter
            //        ? card.Suit == Suit.Spades ? cards.Last().Suit != Suit.Hearts : card.Suit == Suit.Hearts ? cards.Last().Suit != Suit.Spades : card.Suit == Suit.Diamonds ? cards.Last().Suit != Suit.Clubs : cards.Last().Suit != Suit.Diamonds
            //        : card.Suit == Suit.Spades ? cards.Last().Suit != Suit.Diamonds : card.Suit == Suit.Diamonds ? cards.Last().Suit != Suit.Spades : card.Suit == Suit.Hearts ? cards.Last().Suit != Suit.Clubs : cards.Last().Suit != Suit.Hearts,

            // Rule 3: Ranks must alternate between being divisible by 𝑛 and not.
            (card, cards) => (((int) card.Rank + 1) % divisibleBy == 0) ^ (((int) cards.Last().Rank + 1) % divisibleBy == 0),

            // Rule 4: Consecutive ranks must have a difference of 𝑛 .. (𝑛+1) (with wraparound allowed).
            (card, cards) =>
            {
                var thisRank = (int) card.Rank;
                var lastRank = (int) cards.Last().Rank;
                for (int i = 0; i < 2; i++)
                    if (thisRank == (lastRank + difference + i) % 13 || thisRank == ((lastRank - difference - i) % 13 + 13) % 13)
                        return true;
                return false;
            }

            //// Rule 5: Consecutive cards must have associated suits or ranks
            //(card, cards) =>
            //{
            //    if (suitAssocNum == 0 && card.Suit == cards.Last().Suit)
            //        return true;
            //    else if (suitAssocNum > 0 && card.Suit == suitAssoc[1 ^ Array.IndexOf(suitAssoc, cards.Last().Suit)])
            //        return true;
            //    return permissibleRankDifferences.Contains(Math.Abs((int) card.Rank - (int) cards.Last().Rank));
            //}
        );

        int[] activeRulesIxs;
        do
        {
            activeRulesIxs = Enumerable.Range(0, rules.Length).ToList().Shuffle().Take(_numActiveRules).ToArray();
            var inactiveRulesIxs = Enumerable.Range(0, rules.Length).Except(activeRulesIxs).ToArray();

            _activeRules = activeRulesIxs.Select(i => rules[i]).ToArray();
            _inactiveRules = inactiveRulesIxs.Select(i => rules[i]).ToArray();

            _pile = new List<PlayingCard> { PlayingCard.GetRandom() };
        }
        while (!recurseGeneratePile() || _acceptableCards.Length > 8);

        Debug.LogFormat("[Point of Order #{0}] Pile: {1}.", _moduleId, _pile.JoinString(", "));
        if (_fullLogging)
        {
            Debug.LogFormat("[Point of Order #{0}] Active rules: {1}.", _moduleId, activeRulesIxs.Select(r => r + 1).OrderBy(x => x).JoinString(", "));
            Debug.LogFormat("[Point of Order #{0}] Acceptable cards to play: {1}.", _moduleId, _acceptableCards.JoinString(", "));
        }

        for (int i = 0; i < _pile.Count; i++)
        {
            var card = Main.FindChild("PileCard" + (i + 1));
            card.GetComponent<MeshRenderer>().material.mainTexture = getTexture(_pile[i]);
            card.GetComponent<MeshRenderer>().sortingOrder = -_pile.Count + i - 1;
            card.localPosition = new Vector3(interp(-.05f, .05f, i, _pile.Count), .01501f, .045f - .005f * i);
            card.localRotation = Quaternion.Euler(90, -25 + 10f * i + Rnd.Range(0f, 5f), 0);
        }

        var backfaceTexture = new Texture2D(2, 2);
        backfaceTexture.LoadImage(RawPngs.BackData[Rnd.Range(0, RawPngs.BackData.Length)]);

        _choiceCards = new KMSelectable[_numChoiceCards];
        _choiceCardCards = new Transform[_numChoiceCards];
        _choiceCardHighlights = new Transform[_numChoiceCards];
        _frontFaces = new MeshRenderer[_numChoiceCards];
        _backFaces = new MeshRenderer[_numChoiceCards];

        for (int i = 0; i < _numChoiceCards; i++)
            setupChoiceCard(i, backfaceTexture);
    }

    private Texture2D getTexture(PlayingCard card)
    {
        var tx = new Texture2D(2, 2);
        tx.LoadImage(RawPngs.CardData[card.Suit][card.Rank]);
        return tx;
    }

    private void setupChoiceCard(int index, Texture backfaceTexture)
    {
        var card = Main.FindChild("ChoiceCard" + (index + 1));
        card.localPosition = new Vector3(interp(-.062f, .062f, index, _numChoiceCards - 1), .01501f, -.045f);
        card.FindChild("Highlight").FindChild("Highlight(Clone)").GetComponent<MeshRenderer>().sortingOrder = -2;

        _choiceCardCards[index] = card.FindChild("Card");

        _frontFaces[index] = _choiceCardCards[index].FindChild("FrontFace").GetComponent<MeshRenderer>();
        _frontFaces[index].enabled = false;
        _frontFaces[index].sortingOrder = -1;

        _backFaces[index] = _choiceCardCards[index].FindChild("BackFace").GetComponent<MeshRenderer>();
        _backFaces[index].material.mainTexture = backfaceTexture;
        _backFaces[index].sortingOrder = -1;

        _choiceCardHighlights[index] = card.FindChild("Highlight");

        _choiceCards[index] = card.GetComponent<KMSelectable>();
        _choiceCards[index].OnInteract = delegate
        {
            _choiceCards[index].AddInteractionPunch();
            if (_choiceCardLocked[index])
                return false;

            switch (_state)
            {
                case State.FaceDown:
                    StartCoroutine(flipCards());
                    break;

                case State.FaceUp:
                    if (index == _correctCardIndex)
                    {
                        Debug.LogFormat("[Point of Order #{0}] Mao.", _moduleId);
                        Module.HandlePass();
                        _state = State.Solved;
                        for (int i = 0; i < _numChoiceCards; i++)
                            _choiceCardHighlights[i].gameObject.SetActive(false);
                        StartCoroutine(playCard(index));
                    }
                    else
                    {
                        Debug.LogFormat("[Point of Order #{0}] #{1}: Bad card. Point of order.", _moduleId, index + 1);
                        Module.HandleStrike();
                        _state = State.WrongCardPressed;
                    }
                    break;
            }
            return false;
        };
    }

    private IEnumerator flipCards()
    {
        Debug.LogFormat("[Point of Order #{0}] End of point of order.", _moduleId);

        var correctCard = _acceptableCards[Rnd.Range(0, _acceptableCards.Length)];
        //var wrongCards = PlayingCard.AllCards.Except(_acceptableCards).ToList().Shuffle();
        _possibleWrongCards.Shuffle();

        _correctCardIndex = Rnd.Range(0, _numChoiceCards);

        for (int i = 0; i < _numChoiceCards; i++)
        {
            _frontFaces[i].enabled = true;
            _frontFaces[i].material.mainTexture = getTexture(i == _correctCardIndex ? correctCard : _possibleWrongCards[i]);
            Debug.LogFormat("[Point of Order #{0}] Card #{1} = {2}{3}", _moduleId, i + 1, i == _correctCardIndex ? correctCard : _possibleWrongCards[i], _fullLogging ? i == _correctCardIndex ? " (correct)" : " (wrong)" : "");
            StartCoroutine(flipCard(i, flipDown: false));
        }

        _state = State.FaceUp;

        // Wait either for 5 seconds or until the user has pressed a card, whichever comes first
        for (int i = 0; i < 50 && _state == State.FaceUp; i++)
            yield return new WaitForSeconds(.1f);

        if (_state == State.Solved)
            yield break;

        if (_state == State.FaceUp)
        {
            // The user did not play a card in time.
            Debug.LogFormat("[Point of Order #{0}] Failure to play within five seconds.", _moduleId);
            Module.HandleStrike();
        }

        _state = State.Transitioning;

        for (int i = 0; i < _numChoiceCards; i++)
            StartCoroutine(flipCard(i, flipDown: true, isLast: i == _numChoiceCards - 1));
    }

    private IEnumerator flipCard(int index, bool flipDown, bool isLast = false)
    {
        _choiceCardLocked[index] = true;
        yield return new WaitForSeconds(.2f * index);

        foreach (var _ in animationLoop(0, 180, 360, i =>
        {
            _choiceCardCards[index].localRotation = Quaternion.Euler(0, 0, i + (flipDown ? 180 : 0));
            _choiceCardCards[index].localPosition = new Vector3(0, -i * (i - 180) * .00015f, 0);
        }))
            yield return _;

        if (flipDown)
            _frontFaces[index].enabled = false;
        if (isLast)
            _state = State.FaceDown;

        _choiceCardLocked[index] = false;
    }

    private IEnumerable animationLoop(float fromValue, float toValue, float rate, Action<float> action)
    {
        for (float i = fromValue; i <= toValue; i += Time.deltaTime * rate)
        {
            action(i);
            yield return null;
        }
        action(toValue);
    }

    private IEnumerator playCard(int index)
    {
        var startPosition = _choiceCards[index].transform.localPosition;
        var startRotation = _choiceCards[index].transform.localRotation;

        var endPosition = new Vector3(.05f, .01501f, .02f);
        var endRotation = Quaternion.Euler(0, 25 + Rnd.Range(0f, 5f), 0);

        foreach (var _ in animationLoop(0, 1, 2, i =>
        {
            _choiceCards[index].transform.localRotation = Quaternion.Euler(-i * (1 - i) * 180, 0, 0) * Quaternion.Slerp(startRotation, endRotation, i);
            _choiceCards[index].transform.localPosition = Vector3.Lerp(startPosition, endPosition, i) + new Vector3(0, i * (1 - i) * .1f, 0);
        }))
            yield return _;
    }

    private float interp(float from, float to, float t, float dur) { return from + (to - from) / dur * t; }

    private bool recurseGeneratePile()
    {
        // For the first 𝑛−1 cards, only make sure that they satisfy the active rules.
        var permissibleCards = PlayingCard.AllCards.Where(c => !_pile.Contains(c) && _activeRules.All(rule => rule(c, _pile)));

        if (_pile.Count == _numPlayedCards - 1)
        {
            // For the 𝑛th card, also make sure that the pile as a whole doesn’t satisfy any inactive rule
            permissibleCards = permissibleCards.Where(c =>
            {
                var newPile = new List<PlayingCard>();
                var iar = new bool[_inactiveRules.Length];
                for (int i = 0; i < _pile.Count; i++)
                {
                    newPile.Add(_pile[i]);
                    var nextCard = i == _pile.Count - 1 ? c : _pile[i + 1];
                    for (int j = 0; j < _inactiveRules.Length; j++)
                        iar[j] = iar[j] || !_inactiveRules[j](nextCard, newPile);
                }
                return !iar.Contains(false);
            });
        }

        // After the pile is generated, determine which cards are valid to play
        if (_pile.Count == _numPlayedCards)
        {
            _acceptableCards = permissibleCards.ToArray();
            if (_acceptableCards.Length == 0)
                return false;

            // Make sure we can have at least three wrong cards that satisfy all active rules but one
            _possibleWrongCards = PlayingCard.AllCards.Where(c => !_pile.Contains(c) && !_acceptableCards.Contains(c) && _activeRules.Count(rule => rule(c, _pile)) == _activeRules.Length - 1).ToArray();
            if (_possibleWrongCards.Length < _minWrongCards)
                return false;

            return true;
        }

        foreach (var pCard in permissibleCards.ToList().Shuffle())
        {
            _pile.Add(pCard);
            if (recurseGeneratePile())
                return true;
            _pile.RemoveAt(_pile.Count - 1);
        }
        return false;
    }
}
