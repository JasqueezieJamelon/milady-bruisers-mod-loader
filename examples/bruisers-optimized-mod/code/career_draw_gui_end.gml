var _gw = display_get_gui_width();
var _gh = display_get_gui_height();
var _sc = _gh / 480;
draw_set_font(fPixel);
draw_set_halign(fa_left);
draw_set_valign(fa_top);
draw_set_alpha(1);

if (state == 8)
{
    var _ate = variable_global_exists("ateToday") && global.ateToday;
    var _tr = variable_global_exists("trainedToday") && global.trainedToday;
    var _n = variable_global_exists("bom_trainCount") ? global.bom_trainCount : 0;
    if (_tr && _n < 1) _n = 1;

    var _l1 = "ATE TODAY: " + (_ate ? "YES" : "NO");
    var _l2 = "TRAINED TODAY: " + (_tr ? ("YES (x" + string(_n) + ")") : "NO");
    var _lh = string_height("A") * _sc;
    var _w = max(string_width(_l1), string_width(_l2)) * _sc + 12 * _sc;
    var _h = _lh * 2 + 10 * _sc;
    var _x = round(_gw * 0.014);
    var _y = round(_gh * 0.206);

    draw_set_alpha(0.75);
    draw_set_colour(c_black);
    draw_rectangle(_x, _y, _x + _w, _y + _h, false);
    draw_set_alpha(1);
    draw_set_colour(_ate ? c_lime : c_red);
    draw_text_transformed(_x + 6 * _sc, _y + 5 * _sc, _l1, _sc, _sc, 0);
    draw_set_colour(_tr ? c_lime : c_red);
    draw_text_transformed(_x + 6 * _sc, _y + 5 * _sc + _lh, _l2, _sc, _sc, 0);
}

// ---------- v0.3 Meal plan optimizer: hint bar + menu ----------
if (state == 21)
{
    var _hint = "[O / TRIANGLE / Y]  MEAL OPTIMIZER";
    if (variable_global_exists("bom_optTimer") && global.bom_optTimer > 0) _hint = global.bom_optMsg;
    var _hw = string_width(_hint) * _sc + 16 * _sc;
    var _hh = string_height("A") * _sc + 8 * _sc;
    var _hx = round(_gw / 2 - _hw / 2);
    var _hy = round(_gh * 0.86);
    draw_set_alpha(0.85);
    draw_set_colour(c_black);
    draw_rectangle(_hx, _hy, _hx + _hw, _hy + _hh, false);
    draw_set_alpha(1);
    draw_set_colour(c_yellow);
    draw_text_transformed(_hx + 8 * _sc, _hy + 4 * _sc, _hint, _sc, _sc, 0);

    if (variable_global_exists("bom_panel") && global.bom_panel)
    {
        var _tgtOK = !(global.professionalStatus != 1 && global.hasOpponent == 0);
        var _modes = ["NUTRIENTS", "BEST VALUE", "CHEAPEST"];
        var _desc = ["MOST NUTRIENTS, THEN CLOSEST TO GOAL WEIGHT", "NUTRIENTS + GOAL WEIGHT, CHEAPEST", "CLOSEST TO GOAL WEIGHT FOR THE LEAST CASH, EVERY DAY"];
        var _lines = [
            "MODE:  < " + _modes[global.bom_optMode] + " >",
            "GOAL WEIGHT:  < " + string(global.bom_goalW) + " >" + (_tgtOK && global.bom_goalW == global.weightToMake ? "  (TARGET)" : ((_tgtOK || global.bom_goalCustom) ? "  (CUSTOM)" : "  (NO FIGHT - NOT USED)")),
            "OPTIMIZE",
            "CLOSE"
        ];
        var _lh = string_height("A") * _sc * 1.5;
        var _foot = "LEFT/RIGHT CHANGE  L1/R1 +-10  SELECT ON GOAL = USE TARGET";
        var _pw = max(string_width("MEAL OPTIMIZER") * 1.2, string_width(_desc[global.bom_optMode]), string_width(_foot) * 0.8);
        for (var _i = 0; _i < 4; _i++) _pw = max(_pw, string_width("> " + _lines[_i]));
        _pw = _pw * _sc + 24 * _sc;
        var _ph = _lh * 8.2;
        var _px = round(_gw / 2 - _pw / 2);
        var _py = round(_gh / 2 - _ph / 2);
        draw_set_alpha(0.92);
        draw_set_colour(c_black);
        draw_rectangle(_px, _py, _px + _pw, _py + _ph, false);
        draw_set_alpha(1);
        draw_set_colour(c_yellow);
        draw_rectangle(_px, _py, _px + _pw, _py + _ph, true);
        draw_text_transformed(_px + 10 * _sc, _py + 8 * _sc, "MEAL OPTIMIZER", _sc * 1.2, _sc * 1.2, 0);
        draw_set_colour(c_gray);
        draw_text_transformed(_px + 10 * _sc, _py + 8 * _sc + _lh, _desc[global.bom_optMode], _sc, _sc, 0);
        for (var _i = 0; _i < 4; _i++)
        {
            draw_set_colour(global.bom_row == _i ? c_yellow : c_white);
            draw_text_transformed(_px + 10 * _sc, _py + 8 * _sc + _lh * (2.3 + _i), (global.bom_row == _i ? "> " : "  ") + _lines[_i], _sc, _sc, 0);
        }
        draw_set_colour(c_gray);
        draw_text_transformed(_px + 10 * _sc, _py + 8 * _sc + _lh * 6.6, _foot, _sc * 0.8, _sc * 0.8, 0);
    }
}
draw_set_colour(c_white);
