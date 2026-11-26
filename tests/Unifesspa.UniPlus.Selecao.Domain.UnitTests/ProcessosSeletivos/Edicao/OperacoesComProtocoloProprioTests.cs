namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As operações que não passam pela guarda de edição: o congelamento as chama sobre um processo
/// publicado, e a remoção do formulário tem a guarda própria do rascunho.
/// </summary>
public sealed class OperacoesComProtocoloProprioTests
{
    private static readonly Dictionary<string, CopiaDoItemNoCatalogo> CatalogoVazio = [];

    [Fact(DisplayName = "Recopiar os itens do catálogo funciona no processo publicado")]
    public void RecopiarItensDoCatalogo_SemGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _);

        Result resultado = processo.RecopiarItensDoCatalogo(CatalogoVazio, new HashSet<string>());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Recopiar as unidades das fases funciona no processo publicado")]
    public void RecopiarUnidadesDasFases_SemGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _);

        Action recopia = () => processo.RecopiarUnidadesDasFases(new Dictionary<Guid, UnidadeResponsavelSnapshot>());

        recopia.Should().NotThrow();
    }

    [Fact(DisplayName = "Remover formulário em processo publicado recusa pela guarda de edição")]
    public void RemoverFormulario_PublicadoRecusaPelaGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _);

        Result resultado = processo.RemoverFormulario(PapelDoFormularioDeTeste.Inscricao);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Aplicar a proteção vigente funciona no processo publicado e exige a proteção")]
    public void AplicarProtecaoVigente_SemGuarda()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _);

        processo.AplicarProtecaoVigente(
            new ProtecaoVigenteDasExigencias(
                new Dictionary<string, ClassificacaoDeProtecao>(), new Dictionary<Guid, ProtecaoDoDocumento>(), new HashSet<string>(), new HashSet<Guid>()));
        Action semProtecao = () => processo.AplicarProtecaoVigente(null!);

        semProtecao.Should().Throw<ArgumentNullException>();
    }
}
