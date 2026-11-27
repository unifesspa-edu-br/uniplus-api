namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services.ApoioDaAvaliacaoDosDocumentos;

/// <summary>
/// Os grupos de comprovação na avaliação dos documentos da inscrição: o grupo que exige todos os documentos, o grupo dentro de grupo,
/// a repetição por membro e a alternativa dispensada quando o grupo é atendido.
/// </summary>
public sealed class GruposDeComprovacaoNaAvaliacaoDosDocumentosTests
{
    [Fact(DisplayName = "CNH e comprovante compartilham o grupo que exige ambos dentro do grupo alternativo")]
    public void ESobOu_OsDoisDocumentosDividemOE()
    {
        DocumentoExigido rg = Geral(), cnh = Geral(), comprovante = Geral();
        RegraDeComprovacao conjunto = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.TodosOsItens, null, null), 1, [], [RegraDeComprovacao.CriarRegraDeDocumento(cnh, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(comprovante, 1).Value!]).Value!;
        RegraDeComprovacao identidade = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), 0, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, conjunto]).Value!;

        Dictionary<Guid, IReadOnlyList<GrupoDeComprovacao>> grupos = GruposPorDocumento(ComRegrasSemGrupo([identidade]));

        grupos[rg.Id].Should().Equal(new GrupoDeComprovacao(identidade.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null));
        grupos[cnh.Id].Should().Equal(new GrupoDeComprovacao(identidade.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), new GrupoDeComprovacao(conjunto.Id, FormaDeComprovacao.TodosOsItens, null, null));
        grupos[comprovante.Id].Should().Equal(grupos[cnh.Id], "os dois são uma alternativa só, e não duas");
    }

    [Fact(DisplayName = "Quando um grupo contém outro, cada documento traz os dois grupos, do mais externo ao mais interno")]
    public void GrupoDentroDeGrupo_TrazOsDoisGrupos()
    {
        DocumentoExigido a = Geral(), b = Geral(), c = Geral();
        RegraDeComprovacao interno = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), 1, [], [RegraDeComprovacao.CriarRegraDeDocumento(b, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(c, 1).Value!]).Value!;
        RegraDeComprovacao externo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 2, null), 0, [], [RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!, interno]).Value!;

        Dictionary<Guid, IReadOnlyList<GrupoDeComprovacao>> grupos = GruposPorDocumento(ComRegrasSemGrupo([externo]));

        grupos[a.Id].Should().Equal(new GrupoDeComprovacao(externo.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 2, null));
        grupos[b.Id].Should().Equal(new GrupoDeComprovacao(externo.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 2, null), new GrupoDeComprovacao(interno.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null));
        grupos[c.Id].Should().Equal(grupos[b.Id]);
    }

    [Fact(DisplayName = "A repetição pode ser declarada no grupo ou em cada regra de documento")]
    public void Repeticao_DizOndeComeca()
    {
        DocumentoExigido rgX = Geral(), cnhX = Geral(), rgY = Geral(), cnhY = Geral();
        RegraDeComprovacao porMembro = GrupoRepetidoPorMembro(rgX, cnhX);
        RegraDeComprovacao deTodos = GrupoDeRegrasRepetidasPorMembro(rgY, cnhY);

        List<DocumentoAvaliado> documentos = [.. Avaliar(ComRegrasSemGrupo([porMembro, deTodos]), Respostas(), Membros(("m1", true))).Documentos.Itens];

        DocumentoAvaliado x = documentos.Single(d => d.Documento.Id == rgX.Id);
        x.Grupos.Should().Equal(new GrupoDeComprovacao(porMembro.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, Familia));
        x.GrupoDoFormularioParaRepeticao.Should().BeNull("a repetição começa no grupo: cada membro apresenta o RG ou a CNH");
        DocumentoAvaliado y = documentos.Single(d => d.Documento.Id == rgY.Id);
        y.Grupos.Should().Equal(new GrupoDeComprovacao(deTodos.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null));
        y.GrupoDoFormularioParaRepeticao.Should().Be(Familia, "a repetição começa na regra de documento: RG de todos ou CNH de todos");
    }

    [Fact(DisplayName = "A exigência geral repetida por membro faz do grupo dependência da avaliação, com o que decide se ele aparece")]
    public void RegraDeDocumentoRepetidaSemGatilho_OGrupoEDependencia()
    {
        DocumentoExigido doMembro = Geral();
        PredicadoDnf soComBaixaRenda = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar(BaixaRenda, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

        DependenciasDaAvaliacao dependencias = AvaliacaoDaInscricao.Dependencias(
            ComRegrasSemGrupo([RegraDeComprovacao.CriarRegraDeDocumento(doMembro, 0, grupoDoFormularioParaRepeticao: Familia).Value!], exibicaoDaFamilia: soComBaixaRenda));

        dependencias.Grupos.Should().Equal(Familia);
        dependencias.Fatos.Should().Contain(BaixaRenda, "a resposta que mostra o grupo muda a quem se exige");
    }

    [Fact(DisplayName = "O derivado do sistema que decide se o grupo repetido aparece é apurado antes da avaliação")]
    public void ExibicaoDoGrupoRepetido_PedeODerivadoDoSistema()
    {
        PredicadoDnf soParaMenor = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar("FAIXA_ETARIA", Operador.Igual, JsonSerializer.SerializeToElement("MENOR_DE_IDADE")).Value!)]).Value!;

        IReadOnlySet<string> derivados = AvaliacaoDaInscricao.DerivadosDoSistemaCitados(
            ComRegrasSemGrupo([RegraDeComprovacao.CriarRegraDeDocumento(Geral(), 0, grupoDoFormularioParaRepeticao: Familia).Value!], exibicaoDaFamilia: soParaMenor));

        derivados.Should().Contain("FAIXA_ETARIA", "sem ela a consulta não decide se o grupo aparece, e a exigência por membro fica sem resposta");
    }

    private static RegraDeComprovacao GrupoRepetidoPorMembro(DocumentoExigido primeiro, DocumentoExigido segundo) =>
        RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), 0, [], [RegraDeComprovacao.CriarRegraDeDocumento(primeiro, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(segundo, 1).Value!], grupoDoFormularioParaRepeticao: Familia).Value!;

    private static RegraDeComprovacao GrupoDeRegrasRepetidasPorMembro(DocumentoExigido primeiro, DocumentoExigido segundo) =>
        RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), 1, [], [RegraDeComprovacao.CriarRegraDeDocumento(primeiro, 0, grupoDoFormularioParaRepeticao: Familia).Value!, RegraDeComprovacao.CriarRegraDeDocumento(segundo, 1, grupoDoFormularioParaRepeticao: Familia).Value!]).Value!;

    private static Dictionary<Guid, IReadOnlyList<GrupoDeComprovacao>> GruposPorDocumento(ConfiguracaoAvaliavel configuracao) =>
        Avaliar(configuracao, Respostas()).Documentos.Itens.ToDictionary(static d => d.Documento.Id, static d => d.Grupos);

    [Fact(DisplayName = "A alternativa não enviada de um grupo atendido, inclusive de um grupo externo, é dispensada")]
    public void GrupoCumprido_DispensaAsAlternativasNaoEnviadas()
    {
        DocumentoExigido rg = Geral(), cnh = Geral(), passaporte = Geral();
        RegraDeComprovacao interno = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), 1, [], [RegraDeComprovacao.CriarRegraDeDocumento(cnh, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(passaporte, 1).Value!]).Value!;
        RegraDeComprovacao externo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), 0, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, interno]).Value!;

        List<DocumentoAvaliado> comRg = [.. Avaliar(ComRegrasSemGrupo([externo]), Respostas(), apresentacoes: Enviados(rg)).Documentos.Itens];
        List<DocumentoAvaliado> semNada = [.. Avaliar(ComRegrasSemGrupo([externo]), Respostas()).Documentos.Itens];

        comRg.Single(d => d.Documento.Id == rg.Id).DispensadoPeloGrupo.Should().BeFalse("o enviado está atendido, não dispensado");
        comRg.Where(d => d.Documento.Id != rg.Id).Should().OnlyContain(static d => d.DispensadoPeloGrupo, "o RG atende o grupo externo, e a CNH e o passaporte não são necessários");
        semNada.Should().OnlyContain(static d => !d.DispensadoPeloGrupo);
    }

    [Fact(DisplayName = "O grupo atendido por uma alternativa sem repetição dispensa a alternativa repetida por membro, em cada ocorrência")]
    public void GrupoCumpridoForaDaRepeticao_DispensaAAlternativaRepetida()
    {
        DocumentoExigido cadUnico = Geral(), renda = Geral();
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), 0, [], [RegraDeComprovacao.CriarRegraDeDocumento(cadUnico, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(renda, 1, grupoDoFormularioParaRepeticao: Familia).Value!]).Value!;

        List<DocumentoAvaliado> documentos = [.. Avaliar(
            ComRegrasSemGrupo([grupo]), Respostas(), Membros(("m1", true), ("m2", true)), apresentacoes: Enviados(cadUnico)).Documentos.Itens];

        documentos.Where(d => d.Documento.Id == renda.Id).Should().HaveCount(2).And.OnlyContain(static d => d.DispensadoPeloGrupo);
    }
}
