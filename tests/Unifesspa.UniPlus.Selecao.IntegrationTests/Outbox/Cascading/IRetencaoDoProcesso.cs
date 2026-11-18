namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>A parte da sessão que retém a linha do processo que a fila de operações usa: esperar a fila crescer e soltar a linha.</summary>
internal interface IRetencaoDoProcesso
{
    Task AguardarOperacoesEsperandoAsync(int quantidade);

    Task LiberarAsync();
}
