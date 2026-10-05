using System.Windows;
namespace VWP;
public partial class MiniPlayer : Window
{
    public MiniPlayer(){InitializeComponent();CloseButton.Click+=(_,_)=>Hide();Deactivated+=(_,_)=>Hide();}
}
