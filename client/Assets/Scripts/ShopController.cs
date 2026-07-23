using System;
using System.Collections;
using System.Linq;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Shop: spend coins earned from play on cosmetic board and piece-tint unlocks. Coins come only from
// activity (CosmeticShop.CoinsEarned over the quest snapshot); money and coins never buy strength and
// nothing here gates training - these are extra collectibles on top of the always-free themes.
public sealed class ShopController : MonoBehaviour
{
    private VisualElement _root, _listHost;
    private Label _balanceLabel;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }
        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        StartCoroutine(Build(doc));
    }

    private IEnumerator Build(UIDocument doc)
    {
        yield return null;
        _root = doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Row; _root.style.flexGrow = 1; _root.style.backgroundColor = UiKit.Bg;
        _root.Add(UiKit.NavRail("Shop"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 20, 24, 20, 24);

        var head = UiKit.Row();
        head.style.width = 560; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 4;
        head.Add(UiKit.Text_("Shop", 24, UiKit.Text, bold: true));
        _balanceLabel = UiKit.Text_("", 18, UiKit.Gold, bold: true);
        head.Add(_balanceLabel);
        center.Add(head);

        var sub = UiKit.Text_("Coins are earned by playing - solving puzzles, winning games, keeping a streak. Spend them on cosmetics. Nothing here affects your rating or unlocks training.", 13, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.maxWidth = 560; sub.style.marginBottom = 14;
        center.Add(sub);

        var scroll = UiKit.Scroll(); scroll.style.width = 560; scroll.style.flexGrow = 1;
        _listHost = scroll;
        center.Add(scroll);
        _root.Add(center);

        Refresh();

        if (Environment.GetCommandLineArgs().Contains("-kaissa-shoptest"))
            StartCoroutine(AutoDemo());
    }

    private int Balance()
    {
        int earned = CosmeticShop.CoinsEarned(Snapshot());
        return Math.Max(0, earned - KaissaSettings.CoinsSpent);
    }

    private static QuestSnapshot Snapshot()
    {
        PlayerStats stats;
        try { stats = KaissaTrainer.CreateDefault(KaissaProgress.Load()).GetStats(); }
        catch { stats = null; }

        int puzzlesSolved = stats?.TotalCorrect ?? 0;
        int bestStreak = Math.Max(stats?.BestStreak ?? 0, KaissaSettings.PuzzleBestStreak);
        return new QuestSnapshot(
            PuzzlesSolved: puzzlesSolved,
            GamesWon: SafeWins(),
            BestPuzzleStreak: bestStreak,
            DayStreak: KaissaStreak.CurrentDays(),
            BotsBeaten: KaissaSettings.BotsBeaten.Split(',', StringSplitOptions.RemoveEmptyEntries).Length,
            MemoryBest: KaissaSettings.MemoryBest,
            VisualizationBest: KaissaSettings.VisualizationBest,
            SoloBest: KaissaSettings.SoloBest);
    }

    private static int SafeWins()
    {
        try { return KaissaGameLog.Wins; }
        catch { return 0; }
    }

    private void Refresh()
    {
        int balance = Balance();
        _balanceLabel.text = $"{balance} coins";
        _listHost.Clear();

        foreach (var item in CosmeticShop.Catalog)
        {
            bool owned = KaissaSettings.OwnsCosmetic(item.Id);
            bool equipped = IsEquipped(item);
            bool affordable = !owned && balance >= item.Cost;

            var card = new VisualElement();
            card.style.backgroundColor = UiKit.Panel2; UiKit.Radius(card, 12); UiKit.Pad(card, 12, 16, 12, 16);
            card.style.marginBottom = 10; card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween; card.style.alignItems = Align.Center;

            var left = UiKit.Col();
            left.Add(UiKit.Text_(item.Name, 16, UiKit.Text, bold: true));
            left.Add(UiKit.Text_($"{Kind(item.Kind)}  -  {item.Cost} coins", 12, UiKit.Dim));
            card.Add(left);

            var right = UiKit.Row();
            if (owned)
            {
                if (equipped)
                    right.Add(UiKit.Text_("Equipped", 13, UiKit.GreenHi, bold: true));
                else
                {
                    var eq = UiKit.Primary("Equip", () => { Equip(item); Refresh(); }, 13);
                    eq.style.width = 110; right.Add(eq);
                }
            }
            else
            {
                var buy = UiKit.Primary($"Buy", () => TryBuy(item), 13);
                buy.style.width = 110; buy.SetEnabled(affordable);
                right.Add(buy);
                if (!affordable)
                    right.Add(UiKit.Text_($"  need {item.Cost - balance} more", 12, UiKit.Mute));
            }
            card.Add(right);
            _listHost.Add(card);
        }
    }

    private void TryBuy(CosmeticItem item)
    {
        var owned = KaissaSettings.OwnedCosmetics.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (CosmeticShop.TryBuy(Balance(), owned, item.Id, out int cost))
        {
            KaissaSettings.CoinsSpent += cost;
            KaissaSettings.MarkCosmeticOwned(item.Id);
            Equip(item); // equip on purchase for immediate feedback
        }
        Refresh();
    }

    private static void Equip(CosmeticItem item)
    {
        if (item.Kind == "board") KaissaSettings.EquippedBoardCosmetic = item.Id;
        else if (item.Kind == "pieces") KaissaSettings.EquippedPiecesCosmetic = item.Id;
    }

    private static bool IsEquipped(CosmeticItem item) =>
        (item.Kind == "board" && KaissaSettings.EquippedBoardCosmetic == item.Id)
        || (item.Kind == "pieces" && KaissaSettings.EquippedPiecesCosmetic == item.Id);

    private static string Kind(string kind) => kind switch
    {
        "board" => "Board theme",
        "pieces" => "Piece tint",
        _ => kind,
    };

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneTransition.Go("Menu");
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "shop.png"));
        // Grant enough coins to buy the cheapest item, then buy it, to show the owned/equipped state.
        KaissaSettings.CoinsSpent = 0;
        KaissaSettings.PuzzleBestStreak = Math.Max(KaissaSettings.PuzzleBestStreak, 0);
        var cheapest = CosmeticShop.Catalog.OrderBy(c => c.Cost).First();
        KaissaSettings.MarkCosmeticOwned(cheapest.Id);
        Equip(cheapest);
        Refresh();
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "shop_owned.png"));
        yield return new WaitForSeconds(0.3f);
        Application.Quit();
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
        return null;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
