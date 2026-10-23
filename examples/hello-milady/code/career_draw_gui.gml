// Runs at the end of the career screen's Draw GUI event, after the game's own code.
// state == 8 is the Career Management screen.
if (state == 8)
{
    var _sc = display_get_gui_height() / 480;
    draw_set_font(fPixel);
    draw_set_halign(fa_right);
    draw_set_valign(fa_bottom);
    draw_set_colour(c_aqua);
    draw_text_transformed(display_get_gui_width() - 6 * _sc, display_get_gui_height() - 2 * _sc, "HELLO MILADY EXAMPLE MOD", _sc * 0.6, _sc * 0.6, 0);
    draw_set_halign(fa_left);
    draw_set_valign(fa_top);
    draw_set_colour(c_white);
}
