namespace Unifesspa.UniPlus.Selecao.ArchTests.Amostras.ComCampo
{
    /// <summary>Política de mentira, com campo reatribuível, para provar que a regra A3 o detecta.</summary>
    internal sealed class PoliticaComCampo
    {
        private int _contador;

        internal int Conta() => ++_contador;
    }
}

namespace Unifesspa.UniPlus.Selecao.ArchTests.Amostras.ComPropriedade
{
    /// <summary>Política de mentira, com propriedade reatribuível, para provar que a regra A3 a detecta.</summary>
    internal sealed class PoliticaComPropriedade
    {
        private readonly string _padrao = "padrão";

        public string Nome { get; set; } = string.Empty;

        internal string NomeOuPadrao() => Nome.Length > 0 ? Nome : _padrao;
    }
}
