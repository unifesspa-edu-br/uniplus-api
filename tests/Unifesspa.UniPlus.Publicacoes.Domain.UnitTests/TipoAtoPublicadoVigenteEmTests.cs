namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// A janela de vigência de uma versão do tipo de ato é semiaberta, <c>[início, fim)</c>: o início
/// vale e o fim não, e a versão sem fim não encerra.
/// </summary>
public sealed class TipoAtoPublicadoVigenteEmTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly Fim = new(2026, 6, 1);

    [Theory(DisplayName = "Versão com fim vigora do início até a véspera do fim")]
    [InlineData(2025, 12, 31, false)]
    [InlineData(2026, 1, 1, true)]
    [InlineData(2026, 3, 15, true)]
    [InlineData(2026, 5, 31, true)]
    [InlineData(2026, 6, 1, false)]
    [InlineData(2026, 6, 2, false)]
    public void VigenteEm_VersaoComFim_RespeitaInicioInclusivoEFimExclusivo(int ano, int mes, int dia, bool esperado)
    {
        TipoAtoPublicado tipo = Versao(vigenciaFim: Fim);

        bool vigente = TipoAtoPublicado.VigenteEm(new DateOnly(ano, mes, dia)).Compile()(tipo);

        vigente.Should().Be(esperado);
    }

    [Theory(DisplayName = "Versão sem fim vigora a partir do início, sem encerrar")]
    [InlineData(2025, 12, 31, false)]
    [InlineData(2026, 1, 1, true)]
    [InlineData(2076, 1, 1, true)]
    public void VigenteEm_VersaoSemFim_VigoraDoInicioEmDiante(int ano, int mes, int dia, bool esperado)
    {
        TipoAtoPublicado tipo = Versao(vigenciaFim: null);

        bool vigente = TipoAtoPublicado.VigenteEm(new DateOnly(ano, mes, dia)).Compile()(tipo);

        vigente.Should().Be(esperado);
    }

    [Theory(DisplayName = "A expressão concorda com EstaVigenteEm em toda a tabela de bordas")]
    [InlineData(2025, 12, 31)]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 5, 31)]
    [InlineData(2026, 6, 1)]
    public void VigenteEm_ComparadaComEstaVigenteEm_DaORespostaIgual(int ano, int mes, int dia)
    {
        TipoAtoPublicado tipo = Versao(vigenciaFim: Fim);
        var data = new DateOnly(ano, mes, dia);

        TipoAtoPublicado.VigenteEm(data).Compile()(tipo).Should().Be(tipo.EstaVigenteEm(data));
    }

    private static TipoAtoPublicado Versao(DateOnly? vigenciaFim) =>
        TipoAtoPublicado.Criar(
            codigo: "AVISO",
            nome: "Aviso",
            congelaConfiguracao: false,
            unicoPorObjeto: false,
            efeitoIrreversivel: false,
            ehResultado: false,
            vigenciaInicio: Inicio,
            vigenciaFim: vigenciaFim,
            baseLegal: null).Value!;
}
