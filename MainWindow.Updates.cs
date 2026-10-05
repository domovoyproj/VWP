using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
namespace VWP;
public partial class MainWindow
{
    UpdateRelease? availableUpdate;
    async Task CheckForUpdates()
    {
        Settings.CheckUpdateButton.IsEnabled=false;Settings.UpdateStatus.Text="Проверяю релизы…";
        try
        {
            availableUpdate=await UpdateService.Check();
            Settings.UpdateStatus.Text=availableUpdate is null?"Установлена актуальная версия":"Доступна версия "+availableUpdate.Version;
            Settings.InstallUpdateButton.Visibility=availableUpdate is null?Visibility.Collapsed:Visibility.Visible;
        }
        catch(Exception e){Settings.UpdateStatus.Text="Проверка недоступна: "+e.Message;}
        finally{Settings.CheckUpdateButton.IsEnabled=true;}
    }
    async Task InstallUpdate()
    {
        if(availableUpdate is null)return;Settings.InstallUpdateButton.IsEnabled=false;Settings.CheckUpdateButton.IsEnabled=false;
        try
        {
            string installer=await UpdateService.Download(availableUpdate,new Progress<double>(value=>Settings.UpdateStatus.Text=$"Скачивание · {value:0}%"));
            Settings.UpdateStatus.Text="Файл проверен. Запускаю установку…";
            var start=new ProcessStartInfo(installer){UseShellExecute=true};start.ArgumentList.Add("/NORESTART");
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"unins000.exe")))start.ArgumentList.Add("/DIR="+AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            Process.Start(start);exiting=true;Close();
        }
        catch(Exception e){Settings.UpdateStatus.Text="Обновление: "+e.Message;Settings.InstallUpdateButton.IsEnabled=true;Settings.CheckUpdateButton.IsEnabled=true;}
    }
}
