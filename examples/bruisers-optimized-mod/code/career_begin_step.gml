// One-time migration from the standalone mod (v0.3.1 and earlier used "mod" names).
if (!variable_global_exists("bom_migrated"))
{
    global.bom_migrated = true;
    if (variable_global_exists("modTrainCount")) global.bom_trainCount = global.modTrainCount;
    if (variable_global_exists("modTrainDay")) global.bom_trainDay = global.modTrainDay;
    if (variable_global_exists("modGoalW")) global.bom_goalW = global.modGoalW;
    if (variable_global_exists("modGoalCustom")) global.bom_goalCustom = global.modGoalCustom;
    if (variable_global_exists("modOptMode")) global.bom_optMode = global.modOptMode;
}
if (!variable_global_exists("bom_trainCount")) global.bom_trainCount = 0;

// Have the game's own save system store the training count (saved/loaded by name).
if (variable_global_exists("saveVarsGame") && is_array(global.saveVarsGame))
{
    if (array_get_index(global.saveVarsGame, "bom_trainCount") < 0) array_push(global.saveVarsGame, "bom_trainCount");
    if (array_get_index(global.saveVarsGame, "bom_trainDay") < 0) array_push(global.saveVarsGame, "bom_trainDay");
    if (array_get_index(global.saveVarsGame, "bom_goalW") < 0) array_push(global.saveVarsGame, "bom_goalW");
    if (array_get_index(global.saveVarsGame, "bom_goalCustom") < 0) array_push(global.saveVarsGame, "bom_goalCustom");
    if (array_get_index(global.saveVarsGame, "bom_optMode") < 0) array_push(global.saveVarsGame, "bom_optMode");
}

// Day stamp: if the count belongs to a different day (new day, or a save without it), rebuild it.
// (Day of the week only: picking an opponent jumps 10 weeks ahead but it's still the same day.)
var _dk = variable_global_exists("activeDay") ? "d" + string(global.activeDay) : "d";
if (!variable_global_exists("bom_trainDay") || global.bom_trainDay != _dk)
{
    global.bom_trainDay = _dk;
    global.bom_trainCount = global.trainedToday ? 1 : 0;
}

global.bom_watch = (state == 4);
if (global.bom_watch)
{
    global.bom_was = global.trainedToday;
    global.trainedToday = 0;
}
