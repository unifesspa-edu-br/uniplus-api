namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="NoExigencia"/> (Story #920): fábricas (<see cref="NoExigencia.CriarFolha"/>/
/// <see cref="NoExigencia.CriarGrupo"/>), invariantes de cadastro (grupo não vazio, sem ciclo,
/// mesma fase, cardinalidade/consequência/base legal por tipo de nó) e os métodos de leitura
/// estrutural (<see cref="NoExigencia.FaseComum"/>/<see cref="NoExigencia.DeterminaResultado"/>/
/// <see cref="NoExigencia.PodeAlcancarModalidade"/>/<see cref="NoExigencia.AchatarComDescendentes"/>).
/// </summary>
public sealed class NoExigenciaTests
{
    private static DocumentoExigido Documento(
        Guid? faseId = null, string? consequencia = null, bool obrigatorio = true, Aplicabilidade aplicabilidade = Aplicabilidade.Geral) =>
        DocumentoExigido.Criar(
            faseId ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "IDENTIDADE",
            "Documento de identidade",
            "PESSOAL",
            aplicabilidade,
            obrigatorio,
            consequencia,
            [],
            [],
            null,
            FormatosPermitidos.Criar(true, null).Value!,
            null).Value!;

    private static NoExigenciaBaseLegal BaseLegalResolvida() =>
        NoExigenciaBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;

    [Fact(DisplayName = "CriarFolha aceita um DocumentoExigido válido")]
    public void CriarFolha_DocumentoValido_Aceita()
    {
        DocumentoExigido documento = Documento();

        Result<NoExigencia> resultado = NoExigencia.CriarFolha(documento, ordem: 0);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Tipo.Should().Be(TipoNo.Folha);
        resultado.Value!.DocumentoExigidoId.Should().Be(documento.Id);
        resultado.Value!.DocumentoExigido.Should().BeSameAs(documento);
        resultado.Value!.Filhos.Should().BeEmpty();
    }

    [Fact(DisplayName = "CriarFolha recusa ordem negativa")]
    public void CriarFolha_OrdemNegativa_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: -1);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OrdemInvalida");
    }

    [Theory(DisplayName = "CriarGrupo recusa grupo sem filhos, E ou OU")]
    [InlineData(TipoNo.GrupoE)]
    [InlineData(TipoNo.GrupoOu)]
    public void CriarGrupo_SemFilhos_Recusa(TipoNo tipo)
    {
        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(tipo, 0, null, null, [], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.GrupoVazio");
    }

    [Fact(DisplayName = "CriarGrupo recusa filhos de fases diferentes")]
    public void CriarGrupo_FasesDiferentes_Recusa()
    {
        NoExigencia folhaA = NoExigencia.CriarFolha(Documento(Guid.CreateVersion7()), 0).Value!;
        NoExigencia folhaB = NoExigencia.CriarFolha(Documento(Guid.CreateVersion7()), 1).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [folhaA, folhaB]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.GrupoComFasesDiferentes");
    }

    [Fact(DisplayName = "CriarGrupo aceita filhos da mesma fase")]
    public void CriarGrupo_MesmaFase_Aceita()
    {
        Guid faseId = Guid.CreateVersion7();
        NoExigencia folhaA = NoExigencia.CriarFolha(Documento(faseId), 0).Value!;
        NoExigencia folhaB = NoExigencia.CriarFolha(Documento(faseId), 1).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [folhaA, folhaB]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.FaseComum().Should().Be(faseId);
    }

    [Fact(DisplayName = "CriarGrupo recusa ciclo (identidade de referência) — defesa em profundidade de chamador interno")]
    public void CriarGrupo_ComCiclo_Recusa()
    {
        Guid faseId = Guid.CreateVersion7();
        NoExigencia filho = NoExigencia.CriarFolha(Documento(faseId), 0).Value!;

        // Reusa a MESMA instância de nó como filho em duas posições diferentes — não
        // alcançável via o comando HTTP (árvore por valor), mas o invariante SHALL da spec
        // é de domínio, não só de contrato (ver remarks de NoExigencia.CriarGrupo).
        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [filho, filho]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.ArvoreComCiclo");
    }

    [Fact(DisplayName = "CriarGrupo GrupoE recusa quantidadeMinima")]
    public void CriarGrupo_GrupoE_ComQuantidadeMinima_Recusa()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, 1, null, [], [filho]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaProibidaEmGrupoE");
    }

    [Fact(DisplayName = "CriarGrupo GrupoE recusa consequência")]
    public void CriarGrupo_GrupoE_ComConsequencia_Recusa()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, "ELIMINA", [], [filho]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.ConsequenciaProibidaEmGrupoE");
    }

    [Fact(DisplayName = "CriarGrupo GrupoE recusa base legal")]
    public void CriarGrupo_GrupoE_ComBaseLegal_Recusa()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [BaseLegalResolvida()], [filho]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.BaseLegalProibidaEmGrupoE");
    }

    [Theory(DisplayName = "CriarGrupo GrupoOu recusa quantidadeMinima fora dos limites")]
    [InlineData(0)]
    [InlineData(3)]
    public void CriarGrupo_GrupoOu_QuantidadeMinimaForaDosLimites_Recusa(int quantidadeMinima)
    {
        Guid faseId = Guid.CreateVersion7();
        NoExigencia folhaA = NoExigencia.CriarFolha(Documento(faseId), 0).Value!;
        NoExigencia folhaB = NoExigencia.CriarFolha(Documento(faseId), 1).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, quantidadeMinima, null, [], [folhaA, folhaB]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaForaDosLimites");
    }

    [Fact(DisplayName = "CriarGrupo GrupoOu sem quantidadeMinima aplica default 1")]
    public void CriarGrupo_GrupoOu_SemQuantidadeMinima_AplicaDefault()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [filho]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.QuantidadeMinima.Should().Be(NoExigencia.QuantidadeMinimaPadrao);
    }

    [Fact(DisplayName = "CriarGrupo GrupoOu recusa consequência fora do vocabulário fechado")]
    public void CriarGrupo_GrupoOu_ConsequenciaInvalida_Recusa()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, "QUALQUER_COISA", [], [filho]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.ConsequenciaInvalida");
    }

    [Fact(DisplayName = "CriarGrupo GrupoOu recusa base legal sem consequência")]
    public void CriarGrupo_GrupoOu_BaseLegalSemConsequencia_Recusa()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [BaseLegalResolvida()], [filho]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.BaseLegalSemConsequencia");
    }

    [Fact(DisplayName = "CriarGrupo GrupoOu com consequência e base legal — 1ª classe")]
    public void CriarGrupo_GrupoOu_ComConsequenciaEBaseLegal_Aceita()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [BaseLegalResolvida()], [filho]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Consequencia.Should().Be("ELIMINA");
        resultado.Value!.BasesLegais.Should().ContainSingle();
        resultado.Value!.DeterminaResultado().Should().BeTrue();
    }

    [Fact(DisplayName = "DeterminaResultado é sempre falso em grupo E (transparente)")]
    public void DeterminaResultado_GrupoE_SempreFalso()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;
        NoExigencia grupoE = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [filho]).Value!;

        grupoE.DeterminaResultado().Should().BeFalse();
    }

    [Fact(DisplayName = "DeterminaResultado é falso em grupo OU sem consequência")]
    public void DeterminaResultado_GrupoOuSemConsequencia_Falso()
    {
        NoExigencia filho = NoExigencia.CriarFolha(Documento(), 0).Value!;
        NoExigencia grupoOu = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [filho]).Value!;

        grupoOu.DeterminaResultado().Should().BeFalse();
    }

    [Fact(DisplayName = "AchatarComDescendentes devolve o próprio nó e todos os descendentes")]
    public void AchatarComDescendentes_ArvoreProfunda_DevolveTodosOsNos()
    {
        Guid faseId = Guid.CreateVersion7();
        NoExigencia rg = NoExigencia.CriarFolha(Documento(faseId), 0).Value!;
        NoExigencia cpf = NoExigencia.CriarFolha(Documento(faseId), 1).Value!;
        NoExigencia grupoE = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [rg, cpf]).Value!;
        NoExigencia cin = NoExigencia.CriarFolha(Documento(faseId), 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [grupoE, cin]).Value!;

        List<NoExigencia> nos = [.. raiz.AchatarComDescendentes()];

        nos.Should().HaveCount(5);
        nos.Should().Contain(new[] { raiz, grupoE, rg, cpf, cin });
    }

    [Fact(DisplayName = "PodeAlcancarModalidade em grupo é a união (OR) das folhas descendentes")]
    public void PodeAlcancarModalidade_Grupo_UniaoDasFolhas()
    {
        Guid faseId = Guid.CreateVersion7();
        DocumentoExigido docGeral = Documento(faseId, aplicabilidade: Aplicabilidade.Geral);
        NoExigencia folha = NoExigencia.CriarFolha(docGeral, 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [folha]).Value!;

        // Aplicabilidade GERAL alcança qualquer modalidade (DocumentoExigido.PodeAlcancarModalidade).
        grupo.PodeAlcancarModalidade("AC").Should().BeTrue();
    }
}
