using System;
using System.Collections;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Opening repertoire trainer: recall your own moves from your repertoire, scheduled with spaced
// repetition. Each card is one position where it is your turn; the opponent's moves are already made.
// A correct recall pushes the next review out; a wrong one shows the book move and resurfaces it soon.
public sealed class OpeningController : MonoBehaviour
{
    private OpeningProgress _progress;
    private RepertoireSession _session;
    private RepertoireCard _card;

    private Transform _boardRoot;
    private Text _prompt;
    private BoardInteractor _interactor;
    private PieceAudio _audio;
    private bool _busy;
    private float _shownTime;

    private void Start()
    {
        Board3D.SetupScene();

        var canvas = Hud.Canvas();
        _prompt = Hud.Text(canvas, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1000f, 60f));
        Hud.Text(canvas, "Recall your repertoire move. Esc — menu", 18, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(800f, 30f));

        _audio = PieceAudio.Attach(gameObject);
        _interactor = gameObject.AddComponent<BoardInteractor>();
        _interactor.Init(uci => OnPlayerMove(uci), _audio);

        _progress = KaissaOpenings.Load();
        _session = new RepertoireSession(OpeningRepertoire.Default, _progress, new SystemClock());
        NextCard();
    }

    private void NextCard()
    {
        _card = _session.Next();
        if (_card == null)
        {
            _prompt.text = "No repertoire lines.";
            return;
        }
        _shownTime = Time.time;
        ShowPosition(BoardView.FromFen(_card.Fen), canMove: true);
        _prompt.text = $"{_card.LineName} — your move   ·   {_session.DueCount} due";
    }

    private void ShowPosition(BoardView view, bool canMove)
    {
        if (_boardRoot != null)
            Destroy(_boardRoot.gameObject);
        _boardRoot = Board3D.Render(view);
        Board3D.OrientCamera(_card.WhiteToMove); // the side to recall sits at the bottom
        _interactor.OnBoardRendered(_boardRoot, view, lastMoveUci: null, humanCanMove: canMove);
    }

    // The interactor only reports legal moves; the session decides whether it is the book move.
    private void OnPlayerMove(string uci)
    {
        if (_busy || _card == null)
            return;
        _busy = true;
        _interactor.SetInputEnabled(false);

        var result = _session.Submit(uci, TimeSpan.FromSeconds(Time.time - _shownTime));
        KaissaOpenings.Save(_progress);

        var afterFen = ApplyMove(_card.Fen, uci);
        if (afterFen != null)
            ShowPosition(BoardView.FromFen(afterFen), canMove: false);

        if (result.Correct)
        {
            _audio.PlayCorrect();
            _prompt.text = $"{_card.LineName} — correct";
        }
        else
        {
            _audio.PlayWrong();
            _prompt.text = $"{_card.LineName} — book move was {result.ExpectedMove}";
        }

        StartCoroutine(NextAfter(result.Correct ? 0.8f : 1.7f));
    }

    private IEnumerator NextAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _busy = false;
        NextCard();
    }

    private static string ApplyMove(string fen, string uci)
    {
        try
        {
            var game = ChessGame.FromFen(fen);
            if (game.TryMakeMove(uci))
                return game.Fen;
        }
        catch { /* fall through */ }
        return null;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }
}
