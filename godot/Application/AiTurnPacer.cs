namespace Empire.Application;

/// <summary>Post-settlement pacing only; battle state and action selection stay in the session.</summary>
public sealed class AiTurnPacer
{
    public bool IsRunning
    {
        get; private set;
    }
    private bool awaitingSettlement;
    private double? resumeAt;
    private int pauseMilliseconds;
    private int steps;
    private bool exhausted;

    public void Cancel()
    {
        IsRunning = false;
        awaitingSettlement = false;
        resumeAt = null;
        steps = 0;
        exhausted = false;
    }

    public void Tick(double nowMilliseconds, bool computerTurn, bool presenting,
        Func<ComputerStepResult> advance, Func<bool> present, Action<bool> setBusy)
    {
        if (!IsRunning)
        {
            if (!computerTurn)
            {
                exhausted = false;
                return;
            }
            if (presenting || exhausted)
                return;
            IsRunning = true;
            steps = 0;
            setBusy(true);
        }
        try
        {
            if (awaitingSettlement)
            {
                if (presenting)
                    return;
                awaitingSettlement = false;
                resumeAt = nowMilliseconds + pauseMilliseconds;
            }
            if (resumeAt is { } deadline)
            {
                if (nowMilliseconds < deadline)
                    return;
                resumeAt = null;
            }
            if (!computerTurn)
            {
                Finish(setBusy);
                return;
            }
            if (presenting)
                return;
            if (++steps > 2000)
            {
                exhausted = true;
                Finish(setBusy);
                return;
            }
            var result = advance();
            pauseMilliseconds = result.AttemptedHandsOffTurn ? 120 : 90;
            awaitingSettlement = present();
            if (!awaitingSettlement)
                resumeAt = nowMilliseconds + pauseMilliseconds;
        }
        catch
        {
            exhausted = true;
            Finish(setBusy);
            throw;
        }
    }

    private void Finish(Action<bool> setBusy)
    {
        IsRunning = false;
        awaitingSettlement = false;
        resumeAt = null;
        setBusy(false);
    }
}
