using Engine.Core;

namespace MoNoon.Decks;

public sealed class DeckSource : GameBehaviour
{
    public TableReference Catalog;
    public int InitialSeed = 42;
    public CardDeck Deck { get; private set; }

    public override void OnCreate(ScriptLife life) =>
        Deck = new CardDeck(life.Objects.ReadTable(Catalog), InitialSeed);
}
