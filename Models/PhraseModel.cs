using Mobius.Utils;

namespace Mobius.Models
{
    public sealed class PhraseModel : ObservableObject
    {
        private string _text;

        public PhraseModel() { }
        public PhraseModel(string text) => _text = text;

        public string Text
        {
            get => _text;
            set => Set(ref _text, value);
        }

        public override string ToString() => Text;
    }
}
