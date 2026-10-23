if (!variable_global_exists("bom_trainCount")) global.bom_trainCount = 0;
if (variable_global_exists("bom_watch") && global.bom_watch)
{
    if (global.trainedToday)
    {
        global.bom_trainCount = max(global.bom_trainCount, global.bom_was ? 1 : 0) + 1;
        global.trainedToday = 1;
    }
    else
    {
        global.trainedToday = global.bom_was;
    }
    global.bom_watch = false;
}
// v0.3: picking an opponent (pro) replays the game's new-day reset without the day changing,
// which wipes ate/trained. Put them back if it's still the same day.
if (!variable_instance_exists(id, "bom_flagDay"))
{
    bom_flagDay = global.activeDay; bom_prevAte = global.ateToday; bom_prevTr = global.trainedToday;
}
if ((!bom_prevAte && global.ateToday) || (!bom_prevTr && global.trainedToday)) bom_flagDay = global.activeDay;
var _rA = bom_prevAte && !global.ateToday;
var _rT = bom_prevTr && !global.trainedToday;
if (_rA || _rT)
{
    if (global.activeDay == bom_flagDay)
    {
        if (_rA) global.ateToday = 1;
        if (_rT) global.trainedToday = 1;
        global.bom_snap = "";   // re-save with the restored values
    }
    else bom_flagDay = global.activeDay;   // a real new day
}
bom_prevAte = global.ateToday; bom_prevTr = global.trainedToday;

if (!global.trainedToday) global.bom_trainCount = 0;

// The game doesn't save after eating/training, so do it when you're back on the
// Career Management screen and something changed (uses the game's own save function).
var _snap = string(global.ateToday) + "|" + string(global.trainedToday) + "|" + string(global.bom_trainCount);
if (!variable_global_exists("bom_snap")) global.bom_snap = _snap;
if (state == 8 && _snap != global.bom_snap && variable_global_exists("loadIndex"))
{
    global.bom_snap = _snap;
    saveGame("slot", global.loadIndex);
}

// ---------- v0.3 Meal plan optimizer (Meal Plan screen: O / Triangle / Y opens the menu) ----------
if (!variable_global_exists("bom_optTimer")) { global.bom_optTimer = 0; global.bom_optMsg = ""; }
if (!variable_global_exists("bom_optMode") || global.bom_optMode > 2) global.bom_optMode = 1;      // 0 nutrients, 1 best value, 2 cheapest
if (!variable_global_exists("bom_goalCustom")) global.bom_goalCustom = false;
if (!variable_global_exists("bom_panel")) { global.bom_panel = false; global.bom_row = 0; }
if (global.bom_optTimer > 0) global.bom_optTimer -= 1;
var _hasT = !(global.professionalStatus != 1 && global.hasOpponent == 0);
var _W = global.fighterPlayer.weight;
if (!global.bom_goalCustom || !variable_global_exists("bom_goalW")) global.bom_goalW = _hasT ? global.weightToMake : _W;
if (state != 21 && global.bom_panel) global.bom_panel = false;
if (state == 21 && variable_global_exists("mealDay") && variable_global_exists("nutrition"))
{
    var _open = keyboard_check_pressed(ord("O"));
    for (var _p = 0; _p < 4; _p++) if (gamepad_is_connected(_p) && gamepad_button_check_pressed(_p, gp_face4)) _open = true;
    if (!global.bom_panel)
    {
        if (_open && mealMenuState == 0 && confirmDisplay == 0) { global.bom_panel = true; global.bom_row = 2; mealMenuState = 2; }
    }
    else
    {
        if (keyUpPress) global.bom_row = (global.bom_row + 3) mod 4;
        if (keyDownPress) global.bom_row = (global.bom_row + 1) mod 4;
        var _lr = keyRightPress - keyLeftPress;
        var _big = 0;
        if (variable_instance_exists(id, "keyShoulderRightPress")) _big = keyShoulderRightPress - keyShoulderLeftPress;
        if (global.bom_row == 0 && _lr != 0) global.bom_optMode = (global.bom_optMode + _lr + 3) mod 3;
        if (global.bom_row == 1 && (_lr != 0 || _big != 0)) { global.bom_goalW = max(1, global.bom_goalW + _lr + _big * 10); global.bom_goalCustom = !(_hasT && global.bom_goalW == global.weightToMake); }
        if (global.bom_row == 1 && keySelect) { global.bom_goalCustom = false; global.bom_goalW = _hasT ? global.weightToMake : _W; }
        var _run = keySelect && global.bom_row == 2;
        if (keyBack || _open || (keySelect && global.bom_row == 3) || _run) { global.bom_panel = false; mealMenuState = 0; }
        if (_run)
        {
            var _nut = global.nutrition;
            var _days = 0;
            var _m0 = max(0, 4000 - _nut.calorie), _m1 = max(0, 75 - _nut.vitamin), _m2 = max(0, 300 - _nut.mineral), _m3 = max(0, 15 - _nut.water), _m4 = max(0, 30 - _nut.glucose);
            var _need = _m0 / 4000 + _m1 / 75 + _m2 / 300 + _m3 / 15 + _m4 / 30;
            var _cash = variable_global_exists("cash") ? global.cash : 1000000000;
            var _goal = global.bom_goalW;
            var _useGoal = _hasT || global.bom_goalCustom;   // no fight booked and no custom goal: ignore weight
            var _nd = array_length(mealCost);
            var _rows = [];
            var _nmax = 0;
            for (var _a = 0; _a < _nd; _a++)
            for (var _b = _a; _b < _nd; _b++)
            for (var _c = _b; _c < _nd; _c++)
            for (var _d = _c; _d < _nd; _d++)
            {
                var _cost = mealCost[_a] + mealCost[_b] + mealCost[_c] + mealCost[_d];
                if (_cost > _cash) continue;
                var _g0 = mealCalorie[_a] + mealCalorie[_b] + mealCalorie[_c] + mealCalorie[_d];
                var _g1 = mealVitamin[_a] + mealVitamin[_b] + mealVitamin[_c] + mealVitamin[_d];
                var _g2 = mealMineral[_a] + mealMineral[_b] + mealMineral[_c] + mealMineral[_d];
                var _g3 = mealWater[_a] + mealWater[_b] + mealWater[_c] + mealWater[_d];
                var _g4 = mealGlucose[_a] + mealGlucose[_b] + mealGlucose[_c] + mealGlucose[_d];
                var _w = mealWeightIncrease[_a] + mealWeightIncrease[_b] + mealWeightIncrease[_c] + mealWeightIncrease[_d];
                var _nu = min(_g0, _m0) / 4000 + min(_g1, _m1) / 75 + min(_g2, _m2) / 300 + min(_g3, _m3) / 15 + min(_g4, _m4) / 30;
                var _ov = max(0, _g0 - _m0) / 4000 + max(0, _g1 - _m1) / 75 + max(0, _g2 - _m2) / 300 + max(0, _g3 - _m3) / 15 + max(0, _g4 - _m4) / 30;
                array_push(_rows, [_nu, _ov, _cost, _useGoal ? abs(_W + _w - _goal) : 0, _d, _c, _b, _a, _w, 0]);
                if (_nu > _nmax) _nmax = _nu;
            }
            // Goal weight is a ceiling: only consider plans that keep you at or under it.
            // If none do (already over), fall back to the plans that add the least weight.
            var _fit = [];
            var _wmin = 1000000;
            for (var _i = 0; _i < array_length(_rows); _i++) _wmin = min(_wmin, _rows[_i][8]);
            for (var _i = 0; _i < array_length(_rows); _i++)
            {
                var _wk = _rows[_i][8];
                if (!_useGoal || _W + _wk <= _goal || (_W + _wmin > _goal && _wk == _wmin)) array_push(_fit, _rows[_i]);
            }
            _rows = _fit;
            _nmax = 0;
            for (var _i = 0; _i < array_length(_rows); _i++) _nmax = max(_nmax, _rows[_i][0]);
            var _n = array_length(_rows);
            var _pick = undefined;
            var _eps = 0.000001;
            if (global.bom_optMode == 0)
            {
                // NUTRIENTS: fill the most of what's missing, then get closest to goal weight,
                // then least waste, then cheapest
                for (var _i = 0; _i < _n; _i++)
                {
                    var _r = _rows[_i];
                    var _better = false;
                    if (_pick == undefined || _r[0] > _pick[0] + _eps) _better = true;
                    else if (abs(_r[0] - _pick[0]) <= _eps)
                    {
                        if (_r[3] < _pick[3]) _better = true;
                        else if (_r[3] == _pick[3])
                        {
                            if (_r[1] < _pick[1] - _eps) _better = true;
                            else if (abs(_r[1] - _pick[1]) <= _eps && _r[2] < _pick[2]) _better = true;
                        }
                    }
                    if (_better) _pick = _r;
                }
            }
            else
            {
                // BEST VALUE: nutrients + closeness to goal weight, cheapest plan within 5% of the best.
                // CHEAPEST: the same, but only plans you could afford every day until fight night:
                // daily budget = cash / days left (today through Saturday; a full week if no fight is booked).
                var _budget = _cash;
                if (global.bom_optMode == 2)
                {
                    _days = _hasT ? clamp(7 - global.activeDay, 1, 7) : 7;
                    _budget = floor(_cash / _days);
                }
                var _smax = 0;
                for (var _i = 0; _i < _n; _i++)
                {
                    _rows[_i][9] = (_nmax > 0 ? _rows[_i][0] / _nmax : 1) + 1 - min(_rows[_i][3], 10) / 10;
                    if (_rows[_i][2] <= _budget && _rows[_i][9] > _smax) _smax = _rows[_i][9];
                }
                for (var _i = 0; _i < _n; _i++)
                {
                    var _r = _rows[_i];
                    if (_r[2] > _budget || _r[9] < _smax * 0.95) continue;
                    if (_pick == undefined || _r[2] < _pick[2] || (_r[2] == _pick[2] && _r[9] > _pick[9])) _pick = _r;
                }
            }
            if (_pick == undefined)
            {
                global.bom_optMsg = "NOT ENOUGH CASH FOR ANY MEAL";
            }
            else
            {
                var _tc = 0, _tw = 0, _tcal = 0, _tvit = 0, _tmin = 0, _twat = 0, _tglu = 0;
                for (var _sl = 0; _sl < 4; _sl++)
                {
                    var _m = _pick[4 + _sl];
                    global.mealSelectIndex[_sl] = _m;
                    global.mealDay[_sl] = _m;
                    global.mealDayCost[_sl] = mealCost[_m];
                    global.mealDayWeightAdded[_sl] = mealWeightIncrease[_m];
                    global.mealDayCalories[_sl] = mealCalorie[_m];
                    global.mealDayVitamin[_sl] = mealVitamin[_m];
                    global.mealDayMineral[_sl] = mealMineral[_m];
                    global.mealDayWater[_sl] = mealWater[_m];
                    global.mealDayGlucose[_sl] = mealGlucose[_m];
                    _tc += mealCost[_m]; _tw += mealWeightIncrease[_m];
                    _tcal += mealCalorie[_m]; _tvit += mealVitamin[_m]; _tmin += mealMineral[_m]; _twat += mealWater[_m]; _tglu += mealGlucose[_m];
                }
                global.mealPerDayTotalCost = _tc;
                global.mealPerDayWeightIncrease = _tw;
                global.mealPerDayCalorieIncrease = _tcal;
                global.mealPerDayVitaminIncrease = _tvit;
                global.mealPerDayMineralIncrease = _tmin;
                global.mealPerDayWaterIncrease = _twat;
                global.mealPerDayGlucoseIncrease = _tglu;
                var _pct = (_need > 0) ? round(_pick[0] / _need * 100) : 100;
                global.bom_optMsg = "$" + string(_tc) + "   WEIGHT " + string(_W) + " > " + string(_W + _tw) + (_useGoal ? " (GOAL " + string(_goal) + ")" : "") + "   NUTRIENTS NEEDED: " + string(_pct) + "% COVERED";
                if (_days > 0) global.bom_optMsg += "   ($" + string(floor(_cash / _days)) + "/DAY FOR " + string(_days) + " DAY" + (_days == 1 ? "" : "S") + ")";
            }
            global.bom_optTimer = 300;
        }
    }
}
