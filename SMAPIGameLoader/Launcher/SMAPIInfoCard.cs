
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.CardView.Widget;
using System;

namespace SMAPIGameLoader.Launcher;

public class SMAPIInfoCard : CardView
{
    readonly TextView _version;
    readonly TextView _buildCode;
    readonly TextView _buildDate;
    readonly TextView _status;

    Button _installButton;
    Button _uninstallButton;

    public SMAPIInfoCard(Context context) : base(context)
    {
        Radius = Ui.Dp(context, 16);
        CardElevation = Ui.Dp(context, 2);
        UseCompatPadding = true;
        LayoutParameters = new ViewGroup.MarginLayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent)
        {
            // margin ผ่าน SetMargins ของ MarginLayoutParams
        };

        var content = new LinearLayout(context) { Orientation = Orientation.Vertical };
        content.SetPadding(Ui.Dp(context, 20), Ui.Dp(context, 16), Ui.Dp(context, 20), Ui.Dp(context, 16));

        // ---- Header: ชื่อ + badge สถานะ ----
        var header = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = new TextView(context) { Text = "SMAPI" };
        title.SetTextSize(ComplexUnitType.Sp, 18);
        title.SetTypeface(null, TypefaceStyle.Bold);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        _status = new TextView(context) { Text = "Installed" };
        _status.SetTextSize(ComplexUnitType.Sp, 12);
        _status.SetTextColor(Color.White);
        _status.SetPadding(Ui.Dp(context, 10), Ui.Dp(context, 4), Ui.Dp(context, 10), Ui.Dp(context, 4));
        _status.SetBackgroundColor(Color.ParseColor("#2E7D32"));  // เขียว
        header.AddView(_status);

        content.AddView(header);

        // ---- Divider ----
        var divider = new View(context);
        divider.SetBackgroundColor(Color.ParseColor("#22000000"));
        content.AddView(divider, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, Ui.Dp(context, 1))
        { TopMargin = Ui.Dp(context, 12), BottomMargin = Ui.Dp(context, 12) });

        // ---- Key-value rows ----
        _version = AddRow(context, content, "Version");
        _buildCode = AddRow(context, content, "Build Code");
        _buildDate = AddRow(context, content, "Build Date");

        AddView(content);

        // ---- Action buttons ----
        var buttonActionsLayout = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        buttonActionsLayout.SetGravity(GravityFlags.CenterHorizontal);
        buttonActionsLayout.SetPadding(0, Ui.Dp(context, 12), 0, 0);


        var btnUninstall = new Button(context) { Text = "Uninstall" };
        _uninstallButton = btnUninstall;
        btnUninstall.SetAllCaps(false);
        btnUninstall.Click += OnClickUninstall;
        buttonActionsLayout.AddView(btnUninstall, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

        var btnInstall = new Button(context) { Text = "Install" };
        _installButton = btnInstall;
        btnInstall.SetAllCaps(false);
        btnInstall.Click += OnClickInstall;
        buttonActionsLayout.AddView(btnInstall, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        { RightMargin = Ui.Dp(context, 8) });

        content.AddView(buttonActionsLayout);


        // ready
        Refresh();
    }

    private void OnClickUninstall(object sender, EventArgs e)
    {
        SMAPIInstaller.UninstallSMAPI();
        Refresh();
    }

    private void OnClickInstall(object sender, EventArgs e)
    {
        SMAPIInstaller.OnInstalledSMAPI = () =>
        {
            Refresh();
        };
        SMAPIInstaller.OnClickInstallSMAPIZip(sender, e);
    }

    private static TextView AddRow(Context context, LinearLayout parent, string label)
    {
        var row = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        row.SetPadding(0, Ui.Dp(context, 4), 0, Ui.Dp(context, 4));

        var key = new TextView(context) { Text = label };
        key.SetTextSize(ComplexUnitType.Sp, 14);
        key.Alpha = 0.6f;
        row.AddView(key, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        var value = new TextView(context) { Text = "-" };
        value.SetTextSize(ComplexUnitType.Sp, 14);
        value.SetTypeface(null, TypefaceStyle.Bold);
        row.AddView(value);

        parent.AddView(row);



        return value;
    }

    public void Refresh()
    {
        var installed = SMAPIInstaller.IsInstalled;
        _version.Text = $"{SMAPIInstaller.GetCurrentVersion()}";
        _buildCode.Text = $"{SMAPIInstaller.GetBuildCode()}";
        _status.Text = installed ? "Installed" : "Not installed";
        _status.SetBackgroundColor(Color.ParseColor(installed ? "#2E7D32" : "#C62828"));

        {
            var buildCode = SMAPIInstaller.GetBuildCode();
            if (buildCode > 0)
            {
                var buildDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(buildCode);
                _buildDate.Text = $"{buildDateTimeOffset.ToLocalTime():HH:mm:ss dd/MM/yyyy} (d/m/y)";
            }
            else
            {
                _buildDate.Text = "Not available";
            }
        }


        if (SMAPIInstaller.IsInstalled)
        {
            _installButton.Text = "Update SMAPI";
            _uninstallButton.Enabled = true;
        }
        else
        {
            _installButton.Text = "Install SMAPI";
            _uninstallButton.Enabled = false;
        }
    }
}
