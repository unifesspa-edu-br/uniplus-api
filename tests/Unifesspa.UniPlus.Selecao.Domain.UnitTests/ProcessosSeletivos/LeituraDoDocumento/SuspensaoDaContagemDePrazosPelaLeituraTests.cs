namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>
/// A parte que a leitura recusou não esconde nem inventa pendência da contagem dos prazos: o campo recusado não é ausente, e a regra de
/// recurso recusada, que podia distinguir dia útil, deixa a convenção e o calendário sem julgar, com o item do painel pendente.
/// </summary>
public sealed class SuspensaoDaContagemDePrazosPelaLeituraTests
{
    private static readonly ParametrosDeclaradosDoRecurso EmDiasUteis = ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.DiasUteis);

    private static FaseNoDocumento ComRegraEmDiasUteis(string regraCodigo) =>
        Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, AtoDoResultado)) with
        {
            RegraDeRecurso = new(new(regraCodigo, "v1"), AtoDoResultado, EmDiasUteis, []),
        };

    private static IdentificacaoNoDocumento SemConvencao() => Identificacao() with { AlgoritmoContagemPrazo = null };

    private static bool ItemAprovado(IdentificacaoECronogramaLidos lidos, string codigo) =>
        ItensDaContagemDePrazos.Avaliar(Configuracao(lidos), ContextoDeContagemDePrazos.SemCalendario).Single(item => item.Codigo == codigo).Ok;

    [Fact(DisplayName = "A convenção ausente com regra de recurso válida em dias úteis é pendência declarada")]
    public void ConvencaoAusente_ComRegraValida_EPendencia()
    {
        IdentificacaoECronogramaLidos lidos = Ler(SemConvencao(), ComRegraEmDiasUteis(RegraPrazoRecursoCodigo.AncoradoEmAto));

        ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(Configuracao(lidos)).Should().NotBeNull();
    }

    [Fact(DisplayName = "A regra de recurso recusada deixa a pendência da convenção sem julgar e o item do painel pendente")]
    public void RegraRecusada_SuspendeAConvencao()
    {
        IdentificacaoECronogramaLidos lidos = Ler(SemConvencao(), ComRegraEmDiasUteis("REGRA-DIGITADA"));

        ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(Configuracao(lidos)).Should().BeNull();
        ItemAprovado(lidos, "algoritmo_contagem_prazo_nao_declarado").Should().BeFalse();
    }

    [Fact(DisplayName = "A regra de recurso recusada deixa o item do calendário vigente pendente em vez de aprovado")]
    public void RegraRecusada_SuspendeOCalendario()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao(), ComRegraEmDiasUteis("REGRA-DIGITADA"));

        ItemAprovado(lidos, "calendario_vigente_ausente").Should().BeFalse();
    }

    [Fact(DisplayName = "O município recusado pela leitura não é localidade ausente, e o item da localidade fica pendente")]
    public void LocalidadeRecusada_NaoEAusente()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { LocalidadeCodigoIbge = "9999999" });

        ContagemDePrazos.PendenciaDaLocalidade(Configuracao(lidos)).Should().BeNull();
        ItemAprovado(lidos, "localidade_nao_declarada").Should().BeFalse();
    }

    [Fact(DisplayName = "A convenção recusada pela leitura não é convenção ausente, e o item da convenção fica pendente")]
    public void ConvencaoRecusada_NaoEAusente()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { AlgoritmoContagemPrazo = new("CONVENCAO-DIGITADA", "v1") }, ComRegraEmDiasUteis(RegraPrazoRecursoCodigo.AncoradoEmAto));

        ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(Configuracao(lidos)).Should().BeNull();
        ItemAprovado(lidos, "algoritmo_contagem_prazo_nao_declarado").Should().BeFalse();
    }

    [Fact(DisplayName = "O identificador legível recusado pelo domínio não é identificador ausente, e o item fica pendente")]
    public void IdentificadorRecusado_NaoEAusente()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { IdentificadorLegivel = "Identificador Inválido" });

        ContagemDePrazos.PendenciaDoIdentificadorLegivel(Configuracao(lidos)).Should().BeNull();
        ItemAprovado(lidos, "identificador_legivel_nao_declarado").Should().BeFalse();
    }
}
