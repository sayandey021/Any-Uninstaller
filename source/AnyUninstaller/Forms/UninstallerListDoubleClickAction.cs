using AnyUninstaller.Properties;
using Klocman.Localising;

namespace AnyUninstaller.Forms;

public enum UninstallerListDoubleClickAction
{
    [LocalisedName(typeof (Localisable), nameof(Localisable.UninstallerListDoubleClickAction_DoNothing))] DoNothing = 0,
    [LocalisedName(typeof (Localisable),nameof(Localisable.UninstallerListDoubleClickAction_Properties))] OpenProperties,
    [LocalisedName(typeof (Localisable), nameof(Localisable.UninstallerListDoubleClickAction_Uninstall))] Uninstall
}
