using Mobius.Utils;
using System.Runtime.CompilerServices;

namespace Mobius.ViewModels
{
    /// <summary>
    /// Базовый VM для проекта: совместим и с Set(...), и с классическим OnPropertyChanged.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
        protected void OnPropertyChanged([CallerMemberName] string prop = null)
            => Raise(prop);
    }
}
