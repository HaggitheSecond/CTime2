using System;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using CTime2.Strings;
using Microsoft.Toolkit.Uwp;
using ReactiveUI;
using UwCore;
using UwCore.Extensions;

namespace CTime2.Views.UpdateNotes
{
    public class UpdateNotesViewModel : UwCoreScreen
    {
        private string _newVersionInstalled;
        private readonly ObservableAsPropertyHelper<string> _updateNotesHelper;

        public string NewVersionInstalled
        {
            get { return this._newVersionInstalled; }
            set { this.RaiseAndSetIfChanged(ref this._newVersionInstalled, value); }
        }
        public string UpdateNotes => this._updateNotesHelper.Value;

        public UwCoreCommand<string> RefreshUpdateNotes { get; }

        public UpdateNotesViewModel()
        {
            this.NewVersionInstalled = CTime2Resources.GetFormatted("TheNewVersionFormat", Package.Current.Id.Version.ToVersion());

            this.RefreshUpdateNotes = UwCoreCommand.Create(this.RefreshUpdateNotesImpl)
                .HandleExceptions();
            this.RefreshUpdateNotes.ToProperty(this, f => f.UpdateNotes, out this._updateNotesHelper);
        }

        protected override async void OnActivate()
        {
            base.OnActivate();
            await this.RefreshUpdateNotes.ExecuteAsync();
        }

        private async Task<string> RefreshUpdateNotesImpl()
        {
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Views/UpdateNotes/{CTime2Resources.Get("UpdateNotesFileName")}.md"));
                var content = await file.ReadBytesAsync();

                return Encoding.UTF8.GetString(content);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}