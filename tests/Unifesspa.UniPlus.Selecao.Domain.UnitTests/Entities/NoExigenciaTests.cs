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

    [Fact(DisplayName = "CriarFolha recusa quantidadeMinima menor que 1")]
    public void CriarFolha_QuantidadeMinimaMenorQueUm_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: 0, quantidadeMinima: 0);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaDeFolhaInvalida");
    }

    [Fact(DisplayName = "CriarFolha sem chaveDistincao recusa dataReferencia")]
    public void CriarFolha_SemChave_ComDataReferencia_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, dataReferencia: new DateOnly(2026, 1, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.DataReferenciaIndevidaParaChave");
    }

    [Fact(DisplayName = "CriarFolha sem chaveDistincao recusa ocorrenciasEsperadas")]
    public void CriarFolha_SemChave_ComOcorrenciasEsperadas_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, ocorrenciasEsperadas: ["a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasIndevidasParaChave");
    }

    [Theory(DisplayName = "CriarFolha com chave de calendário exige dataReferencia")]
    [InlineData(ChaveDistincao.CompetenciaMensal)]
    [InlineData(ChaveDistincao.ExercicioAnual)]
    public void CriarFolha_ChaveDeCalendario_SemDataReferencia_Recusa(ChaveDistincao chave)
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: 0, chaveDistincao: chave);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario");
    }

    [Fact(DisplayName = "CriarFolha com chave de calendário recusa ocorrenciasEsperadas")]
    public void CriarFolha_ChaveDeCalendario_ComOcorrenciasEsperadas_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0,
            chaveDistincao: ChaveDistincao.CompetenciaMensal,
            dataReferencia: new DateOnly(2026, 1, 1),
            ocorrenciasEsperadas: ["a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasIndevidasParaChave");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa dataReferencia")]
    public void CriarFolha_Ocorrencia_ComDataReferencia_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, chaveDistincao: ChaveDistincao.Ocorrencia, dataReferencia: new DateOnly(2026, 1, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.DataReferenciaIndevidaParaChave");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa ocorrenciasEsperadas vazia")]
    public void CriarFolha_Ocorrencia_ListaVazia_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasVazia");
    }

    [Theory(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa id vazio ou em branco")]
    [InlineData("")]
    [InlineData("   ")]
    public void CriarFolha_Ocorrencia_IdVazioOuEmBranco_Recusa(string idInvalido)
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: ["a", idInvalido]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasComIdVazio");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa ids duplicados")]
    public void CriarFolha_Ocorrencia_IdsDuplicados_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: ["a", "a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasComIdsDuplicados");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa quantidadeMinima divergente do tamanho da lista")]
    public void CriarFolha_Ocorrencia_QuantidadeMinimaDivergente_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 3,
            chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: ["a", "b"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasQuantidadeMinimaDivergente");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia sem lista é modo distinção pura — aceita")]
    public void CriarFolha_Ocorrencia_SemLista_Aceita()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.SlotsEsperados().Should().BeNull();
    }

    [Fact(DisplayName = "SlotsEsperados para CompetenciaMensal deriva as N competências regulares mais recentes")]
    public void SlotsEsperados_CompetenciaMensal_DerivaAsMaisRecentes()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 3,
            chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: new DateOnly(2026, 3, 15));

        resultado.Value!.SlotsEsperados().Should().Equal("2026-03", "2026-02", "2026-01");
    }

    [Fact(DisplayName = "SlotsEsperados para ExercicioAnual deriva os N exercícios regulares mais recentes")]
    public void SlotsEsperados_ExercicioAnual_DerivaOsMaisRecentes()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 3,
            chaveDistincao: ChaveDistincao.ExercicioAnual, dataReferencia: new DateOnly(2026, 3, 15));

        resultado.Value!.SlotsEsperados().Should().Equal("2026", "2025", "2024");
    }

    [Fact(DisplayName = "SlotsEsperados sem chaveDistincao é nulo — contagem bruta")]
    public void SlotsEsperados_SemChave_Nulo()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: 0);

        resultado.Value!.SlotsEsperados().Should().BeNull();
    }

    [Fact(DisplayName = "SlotsEsperados para CompetenciaMensal na âncora mínima representável (ano 1) não lança e não excede o calendário")]
    public void SlotsEsperados_CompetenciaMensal_AncoraMinima_NaoLanca()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 1,
            chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: DateOnly.MinValue);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.SlotsEsperados().Should().Equal("0001-01");
    }

    [Fact(DisplayName = "CriarFolha com CompetenciaMensal recusa quantidadeMinima que excede o calendário representável")]
    public void CriarFolha_CompetenciaMensal_QuantidadeExcedeCalendario_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: DateOnly.MinValue);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaExcedeJanelaRepresentavel");
    }

    [Fact(DisplayName = "CriarFolha com ExercicioAnual recusa quantidadeMinima que excede o calendário representável")]
    public void CriarFolha_ExercicioAnual_QuantidadeExcedeCalendario_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.ExercicioAnual, dataReferencia: new DateOnly(1, 6, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaExcedeJanelaRepresentavel");
    }

    [Theory(DisplayName = "CriarFolha recusa quantidadeMinima fora do teto operacional")]
    [InlineData(0)]
    [InlineData(367)]
    public void CriarFolha_QuantidadeMinimaForaDoTeto_Recusa(int quantidadeInvalida)
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: 0, quantidadeMinima: quantidadeInvalida);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaDeFolhaInvalida");
    }

    [Fact(DisplayName = "CriarFolha copia ocorrenciasEsperadas defensivamente — mutação da lista original não afeta o nó")]
    public void CriarFolha_Ocorrencia_ListaOriginalMutada_NaoAfetaNo()
    {
        List<string> listaOriginal = ["a", "b"];
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: listaOriginal);

        listaOriginal.Add("c");

        resultado.Value!.OcorrenciasEsperadas.Should().Equal("a", "b");
    }

    [Fact(DisplayName = "CriarFolha aceita repetePorEntidade do catálogo fechado")]
    public void CriarFolha_RepetePorEntidade_Aceita()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RepetePorEntidade.Should().Be(TipoEntidade.MembroNucleoFamiliar);
    }

    [Fact(DisplayName = "CriarFolha recusa repetePorEntidade forjado fora do catálogo fechado")]
    public void CriarFolha_RepetePorEntidadeForjado_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, repetePorEntidade: (TipoEntidade)99);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.TipoEntidadeInvalido");
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

    [Fact(DisplayName = "CriarGrupo aceita repetePorEntidade do catálogo fechado")]
    public void CriarGrupo_RepetePorEntidade_Aceita()
    {
        NoExigencia folha = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [folha], repetePorEntidade: TipoEntidade.PessoaJuridicaVinculada);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RepetePorEntidade.Should().Be(TipoEntidade.PessoaJuridicaVinculada);
    }

    [Fact(DisplayName = "CriarGrupo recusa repetePorEntidade forjado fora do catálogo fechado")]
    public void CriarGrupo_RepetePorEntidadeForjado_Recusa()
    {
        NoExigencia folha = NoExigencia.CriarFolha(Documento(), 0).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [folha], repetePorEntidade: (TipoEntidade)99);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.TipoEntidadeInvalido");
    }

    [Fact(DisplayName = "CriarGrupo recusa repetição aninhada — grupo repetido com filho folha também repetido")]
    public void CriarGrupo_RepeticaoAninhadaDireta_Recusa()
    {
        NoExigencia folhaRepetida = NoExigencia.CriarFolha(
            Documento(), 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [folhaRepetida], repetePorEntidade: TipoEntidade.PessoaJuridicaVinculada);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.RepeticaoDeEntidadeAninhada");
    }

    [Fact(DisplayName = "CriarGrupo recusa repetição aninhada mesmo profunda (grupo repetido → E → folha repetida)")]
    public void CriarGrupo_RepeticaoAninhadaProfunda_Recusa()
    {
        NoExigencia folhaRepetida = NoExigencia.CriarFolha(
            Documento(), 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;
        NoExigencia grupoIntermediario = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [folhaRepetida]).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [grupoIntermediario], repetePorEntidade: TipoEntidade.PessoaJuridicaVinculada);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.RepeticaoDeEntidadeAninhada");
    }

    [Fact(DisplayName = "CriarGrupo aceita grupo repetido cujos filhos NÃO repetem")]
    public void CriarGrupo_RepetePorEntidade_ComFilhosNaoRepetidos_Aceita()
    {
        Guid faseId = Guid.CreateVersion7();
        NoExigencia folhaA = NoExigencia.CriarFolha(Documento(faseId), 0).Value!;
        NoExigencia folhaB = NoExigencia.CriarFolha(Documento(faseId), 1).Value!;

        Result<NoExigencia> resultado = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [folhaA, folhaB], repetePorEntidade: TipoEntidade.MembroNucleoFamiliar);

        resultado.IsSuccess.Should().BeTrue();
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
