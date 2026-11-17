namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class UnidadeHistoricoDeIdentificadoresTests
{
    private static readonly DateOnly DataDaTroca = UnidadeDeTeste.InicioDaVigencia.AddMonths(1);

    /// <summary>Muda só os identificadores; nome, cidade, vínculo, tipo e unidade acadêmica ficam os do CEPS.</summary>
    private static Result TrocarIdentificadores(Unidade unidade, MudancaDeIdentificadores mudanca) =>
        unidade.Atualizar(
            unidade.Nome,
            mudanca,
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            false);

    private static void ConferirVigenciaTrocada(
        Unidade unidade, TipoIdentificador tipo, string anterior, string novo, DateOnly dataDaTroca)
    {
        // Entrada antiga fechada na data da mudança.
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == tipo && h.Valor == anterior && h.VigenciaFim == dataDaTroca);

        // Nova entrada aberta.
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == tipo && h.Valor == novo && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Criar abre histórico inicial para Slug, Sigla e Codigo")]
    public void Criar_ComDadosValidos_AbreHistoricoInicialParaIdentificadores()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;

        unidade.Historico.Should().HaveCount(3);
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Slug && h.Valor == "ceps" && h.VigenciaFim == null);
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Sigla && h.Valor == "CEPS" && h.VigenciaFim == null);
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Codigo && h.Valor == "0001" && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Criar com alias abre histórico adicional para Alias")]
    public void Criar_ComAlias_AbreHistoricoParaAlias()
    {
        Unidade unidade = UnidadeDeTeste.Criar(UnidadeDeTeste.IdentificadoresDoCeps(alias: "Centro Seletivos")).Value!;

        unidade.Historico.Should().HaveCount(4);
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Alias && h.Valor == "Centro Seletivos" && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Atualizar com dados válidos altera campos e não toca histórico sem mudança de identificadores")]
    public void Atualizar_SemMudancaDeIdentificadores_NaoAdicionaHistorico()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        int historicoAntes = unidade.Historico.Count;

        Result resultado = UnidadeDeTeste.AtualizarSemMudarIdentificadores(
            unidade, "Centro de Processos Seletivos Alterado", CidadeDaUnidade.Nenhuma, unidadeAcademica: true);

        resultado.IsSuccess.Should().BeTrue();
        unidade.Nome.Should().Be("Centro de Processos Seletivos Alterado");
        unidade.UnidadeAcademica.Should().BeTrue();
        unidade.Historico.Should().HaveCount(historicoAntes, "nenhum identificador mudou");
    }

    [Fact(DisplayName = "Atualizar com motivo de mudança > 500 chars retorna MotivoMudancaTamanho sem alterar a Unidade")]
    public void Atualizar_ComMotivoMuitoLongo_DeveRetornarMotivoMudancaTamanho()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;

        Result resultado = TrocarIdentificadores(
            unidade,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade("ceps-unifesspa", "CEPS", "0001", null), DataDaTroca, new string('m', 501)));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "motivoMudancaIdentificador" && e.Error.Code == UnidadeErrorCodes.MotivoMudancaTamanho);
        unidade.Slug.Valor.Should().Be("ceps");
    }

    [Fact(DisplayName = "Atualizar com motivo de 500 chars mais espaços grava o histórico com 500 chars")]
    public void Atualizar_ComMotivoNoLimiteMaisEspacos_GravaMotivoAparado()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;

        Result resultado = TrocarIdentificadores(
            unidade,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade("ceps-unifesspa", "CEPS", "0001", null), DataDaTroca, new string('m', 500) + "   "));

        resultado.IsSuccess.Should().BeTrue();
        unidade.Historico.Single(h => h.TipoIdentificador == TipoIdentificador.Slug && h.VigenciaFim is null)
            .MotivoMudanca.Should().HaveLength(500);
    }

    [Fact(DisplayName = "Atualizar com novo Slug fecha vigência antiga e abre nova entrada de histórico")]
    public void Atualizar_ComNovoSlug_AdicionaEntradaDeHistorico()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        Slug novoSlug = Slug.From("ceps-unifesspa").Value!;

        Result resultado = TrocarIdentificadores(
            unidade,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade(novoSlug.Valor, "CEPS", "0001", null), DataDaTroca, "Adequação de naming"));

        resultado.IsSuccess.Should().BeTrue();
        unidade.Slug.Should().Be(novoSlug);
        ConferirVigenciaTrocada(unidade, TipoIdentificador.Slug, "ceps", "ceps-unifesspa", DataDaTroca);
    }

    [Fact(DisplayName = "Atualizar troca de Codigo só na caixa (ABC→abc) registra histórico")]
    public void Atualizar_ComCodigoMudandoApenasCaixa_AdicionaEntradaDeHistorico()
    {
        Unidade unidade = UnidadeDeTeste.Criar(
            new IdentificadoresDaUnidade(UnidadeDeTeste.SlugDoCeps.Valor, "CEPS", "ABC", null)).Value!;

        Result resultado = TrocarIdentificadores(
            unidade,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade(UnidadeDeTeste.SlugDoCeps.Valor, "CEPS", "abc", null), DataDaTroca, "Correção de caixa"));

        resultado.IsSuccess.Should().BeTrue();
        unidade.Codigo.Should().Be("abc");
        ConferirVigenciaTrocada(unidade, TipoIdentificador.Codigo, "ABC", "abc", DataDaTroca);
    }
}
