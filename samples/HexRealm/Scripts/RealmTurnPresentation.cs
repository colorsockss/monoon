using System;
using System.Drawing;
using System.Globalization;
using Engine.Core;

public sealed partial class RealmBoard
{
    private enum TurnPhase { None, RivalDelay, RivalMotion, Reports }

    private readonly record struct Training(RealmRules.City Town, RealmRules.Unit Recruit);

    private const float RivalLead = .10f;
    private const float HarvestStart = .34f, HarvestDuration = .30f;
    private const float ResearchStart = .69f, GrowthStart = .87f, ReportsDuration = 1.02f;
    private readonly Training[] training = new Training[RealmRules.CityLimit];
    private TurnPhase turnPhase;
    private float turnAge, rivalMotion, researchGlintTime;
    private int trainingCount, trainingShown, goldBefore, researchBefore, foodBefore, populationBefore;
    private int displayedGold, displayedFood, targetFood, harvestGoal, harvestYield;
    private RealmRules.City? surveyedTown;
    private bool researchShown, growthShown;

    private void BeginTurn()
    {
        turnPhase = TurnPhase.RivalDelay;
        turnAge = 0;
        trainingCount = trainingShown = 0;
        researchShown = growthShown = false;
        rivalMotion = 0;
        displayedGold = goldBefore = rules.Gold;
        researchBefore = rules.Tech;
        surveyedTown = unit < 0 && city >= 0 ? rules.Cities[city] : null;
        displayedFood = foodBefore = surveyedTown?.Food ?? 0;
        populationBefore = surveyedTown?.Population ?? 0;
        objects.Audio.Play(TurnSound);
        session.Screen.For(Camera).Adjust(Color.FromArgb(251, 237, 215), -.025f, .22);
        stampTime = 0;
        ui.SetVisible(TurnStamp, false);
        ClearCursor();
    }

    private void StepTurn(float seconds)
    {
        turnAge += seconds;
        if (turnPhase == TurnPhase.RivalDelay)
        {
            if (turnAge < RivalLead) return;
            var selected = unit >= 0 ? rules.Units[unit] : null;
            rules.Apply(new(OrderKind.End));
            FollowSelection(selected);
            if (surveyedTown is { Side: 1 } town && populationBefore < 8)
            {
                bool grew = town.Population > populationBefore;
                harvestGoal = grew ? town.FoodGoal - 12 : town.FoodGoal;
                targetFood = grew ? harvestGoal : town.Food;
                harvestYield = rules.Forecast(town).Harvest;
            }
            turnPhase = TurnPhase.RivalMotion;
            turnAge = 0;
            UpdatePieces(false);
            Stamp(rivalMotion > 0 ? "Rival turn" : "Rival is moving", Math.Max(.24f, rivalMotion + .04f));
            return;
        }
        if (turnPhase == TurnPhase.RivalMotion)
        {
            if (turnAge < Math.Max(.24f, rivalMotion + .04f)) return;
            session.Screen.For(Camera).Adjust(Color.White, 0, .24);
            turnPhase = TurnPhase.Reports;
            turnAge = 0;
            if (rules.Winner == 0) Stamp($"TURN {rules.Turn}", 1.15f);
            RefreshTurnHud();
            return;
        }

        while (trainingShown < trainingCount &&
               turnAge >= .08f + trainingShown * (.23f / Math.Max(1, trainingCount - 1)))
            ShowTraining(training[trainingShown++]);
        if (turnAge >= HarvestStart) CountHarvest(Math.Clamp((turnAge - HarvestStart) / HarvestDuration, 0, 1));
        if (!researchShown && turnAge >= ResearchStart)
        {
            researchShown = true;
            ShowResearch();
        }
        if (!growthShown && turnAge >= GrowthStart)
        {
            growthShown = true;
            RefreshMap(animateFog: true);
            SetResource(2, LandLabel, $"<color=#245d50><b>{rules.Territory(1)}</b></color> / {rules.LandGoal}");
            RefreshSelection();
            bool claimed = PresentClaims();
            if (rules.Winner != 0)
            {
                turnPhase = TurnPhase.None;
                PresentResult(announce: true, showClaim: claimed);
                session.Log($"HexRealm result {rules.Winner}: {StateHash:x16}");
                LogTurn();
            }
        }
        if (turnPhase != TurnPhase.None && turnAge >= ReportsDuration)
        {
            turnPhase = TurnPhase.None;
            LogTurn();
        }
    }

    private void LogTurn()
    {
        Array.Clear(training);
        trainingCount = trainingShown = 0;
        surveyedTown = null;
        session.Log(FormattableString.Invariant($"HexRealm turn {rules.Turn - 1:D2}: {StateHash:x16}"));
    }

    private void RecordTraining(RealmRules.City town, RealmRules.Unit recruit)
    {
        if (recruit.Side == 1 || visible[RealmRules.Index(recruit.Cell)] || visible[RealmRules.Index(town.Cell)])
            training[trainingCount++] = new(town, recruit);
    }

    private bool TrainingPending(RealmRules.Unit actor)
    {
        for (int i = trainingShown; i < trainingCount; i++)
            if (training[i].Recruit == actor) return true;
        return false;
    }

    private void ShowTraining(Training item)
    {
        var actor = item.Recruit;
        if (!actor.Alive || actor.Side != 1 && !visible[RealmRules.Index(actor.Cell)]) return;
        int index = rules.Units.IndexOf(actor);
        RefreshPiece(index, false);
        PlayRecruitEntrance(index);
        if (actor.Side != 1) return;
        objects.Audio.Play(BuildSound);
        string caption = $"{TownName(item.Town)} trained {UnitNames[(int)actor.Kind]}";
        ShowNotice(caption, .90f);
        if (surveyedTown == item.Town && item.Town.Side == 1)
            objects.GetComponent<RealmButton>(Context)?.SetOption(3, true);
    }

    private void CountHarvest(float portion)
    {
        int gold = goldBefore + (int)MathF.Round((rules.Gold - goldBefore) * portion);
        if (gold != displayedGold)
        {
            displayedGold = gold;
            ui.SetText(GoldLabel, gold.ToString(CultureInfo.InvariantCulture));
        }
        if (growthShown || surveyedTown is not { Side: 1 } || populationBefore >= 8) return;
        int food = foodBefore + (int)MathF.Round((targetFood - foodBefore) * portion);
        if (food == displayedFood) return;
        displayedFood = food;
        SetFood(true, food, harvestGoal, harvestYield);
    }

    private void SetFood(bool growing, int food, int goal, int harvest)
    {
        ui.SetText(Advice, !growing ? "Fully grown" : ui.Preferences.TextScale > 1.5f
            ? $"Food {food}/{goal} +{harvest}/t" : $"Food {food}/{goal} (+{harvest}/turn)");
    }

    private void ShowResearch()
    {
        RefreshResearchHud();
        if (researchBefore == rules.Tech) return;
        if (ui.Preferences.FlashScale > 0)
        {
            researchGlintTime = .22f;
            ui.SetVisible(ResearchGlint, true);
            objects.Tween(ResearchGlint, TweenClock.Ui).UiRect(RectangleF.Empty, 0)
                .UiRect(new(108, 0, 0, 0), researchGlintTime);
        }
        Notify(researchBefore switch
        {
            0 => "Terraces: more food",
            1 => "Fletching: bows ready",
            2 => "Caravans: +2 gold",
            _ => "Charter: stronger walls"
        });
    }

    private void ResetTurnPresentation()
    {
        turnPhase = TurnPhase.None;
        Array.Clear(training);
        trainingCount = trainingShown = 0;
        surveyedTown = null;
        researchGlintTime = 0;
        ui.SetVisible(ResearchGlint, false);
    }
}
