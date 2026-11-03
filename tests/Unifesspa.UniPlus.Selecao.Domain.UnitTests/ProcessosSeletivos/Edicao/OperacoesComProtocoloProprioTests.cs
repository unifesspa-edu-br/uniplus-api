namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;


using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As seis operações que não passam pelo protocolo comum de edição: o congelamento as chama sobre
/// um processo publicado, sem sessão e sem precondição, e a retificação aberta não pode recusá-las
/// nem mover o ETag no meio de um fechamento.
/// </summary>
public sealed class OperacoesComProtocoloProprioTests
{
    private static readonly Dictionary<string, CopiaDoItemNoCatalogo> CatalogoVazio = [];

    [Fact(DisplayName = "Recopiar os itens do catálogo funciona no processo publicado sem sessão e sem precondição")]
    public void RecopiarItensDoCatalogo_SemGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);

        Result resultado = processo.RecopiarItensDoCatalogo(CatalogoVazio, new HashSet<string>());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Recopiar os itens do catálogo sob sessão não move a revisão")]
    public void RecopiarItensDoCatalogo_NaoMoveARevisao()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.EmSessao();

        processo.RecopiarItensDoCatalogo(CatalogoVazio, new HashSet<string>()).IsSuccess.Should().BeTrue();

        processo.Rascunho!.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "Recopiar as unidades das fases funciona sem sessão e sob sessão sem mover a revisão")]
    public void RecopiarUnidadesDasFases_SemGuardaESemRevisao()
    {
        ProcessoSeletivo semSessao = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);
        ProcessoSeletivo emSessao = ProcessoEmEdicaoDeTeste.EmSessao();

        semSessao.RecopiarUnidadesDasFases(new Dictionary<Guid, UnidadeResponsavelSnapshot>());
        emSessao.RecopiarUnidadesDasFases(new Dictionary<Guid, UnidadeResponsavelSnapshot>());

        emSessao.Rascunho!.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "Remover formulário sob sessão recusa pela regra do rascunho, antes de olhar a precondição")]
    public void RemoverFormulario_SobSessaoRecusaAntesDaGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.EmSessao();
        PrecondicaoIfMatch velha = PrecondicaoIfMatch.DeTags(["\"outra-sessao:1\""]);

        Result resultado = processo.RemoverFormulario(FinalidadeFormulario.Inscricao, velha);

        resultado.Error!.Code.Should().Be(FormularioProcessoErrorCodes.RemocaoSoEmRascunho);
        processo.Rascunho!.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "Aplicar modelo sob sessão recusa pela regra do rascunho, antes de olhar a precondição")]
    public void AplicarModelo_SobSessaoRecusaAntesDaGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.EmSessao();
        PrecondicaoIfMatch velha = PrecondicaoIfMatch.DeTags(["\"outra-sessao:1\""]);

        DomainError? recusa = processo.RecusaDeAplicacaoDeModelo(velha);

        recusa!.Code.Should().Be(FormularioProcessoErrorCodes.AplicacaoDeModeloSoEmRascunho);
    }

    [Fact(DisplayName = "Aplicar modelo no processo publicado sem sessão recusa pela guarda")]
    public void AplicarModelo_PublicadoSemSessaoRecusaPelaGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);

        DomainError? recusa = processo.RecusaDeAplicacaoDeModelo(PrecondicaoIfMatch.Ausente);

        recusa!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Limpar a coleta para a restauração funciona no processo publicado sem sessão")]
    public void LimparColeta_SemGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);

        processo.LimparColetaEDerivacaoParaRestauracao();

        processo.Formularios.Should().BeEmpty();
        processo.Campos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Aplicar a proteção vigente funciona no processo publicado sem sessão e exige a proteção")]
    public void AplicarProtecaoVigente_SemGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);

        processo.AplicarProtecaoVigente(
            new ProtecaoVigenteDasExigencias(
                new Dictionary<string, ClassificacaoDeProtecao>(), new Dictionary<Guid, ProtecaoDoDocumento>(), new HashSet<string>()));
        Action semProtecao = () => processo.AplicarProtecaoVigente(null!);

        semProtecao.Should().Throw<ArgumentNullException>();
    }
}
