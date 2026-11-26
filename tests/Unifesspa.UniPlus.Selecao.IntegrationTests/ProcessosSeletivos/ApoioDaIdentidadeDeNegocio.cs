namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaPoliticaDeOrdenacao;

/// <summary>
/// Montagem e conferência dos cenários em que a identidade de negócio decide a ordem do envelope e o
/// conteúdo (nome, descrição) apontaria para a ordem oposta — sem isso, uma política trocada por
/// ordenação de conteúdo passaria despercebida.
/// </summary>
internal static class ApoioDaIdentidadeDeNegocio
{
    /// <summary>
    /// Descrições conflitantes que DIFEREM e PRECEDEM <c>ordem</c> na ordenação alfabética das chaves do
    /// item canônico (<c>descricao</c> &lt; <c>ordem</c> &lt; <c>valorCodigo</c>): com a descrição nula nos
    /// dois itens, a ordenação pelos bytes do item coincidiria com a ordenação por Ordem e um mutante que
    /// trocasse a política por conteúdo passaria despercebido.
    /// </summary>
    internal static void ConferirQueOCodigoOrdenaAoContrarioDaOrdem(ValorDominioDeclaradoCongelado zeta, ValorDominioDeclaradoCongelado alfa) =>
        new[] { zeta, alfa }.OrderBy(static v => v.Codigo, StringComparer.Ordinal).Select(static v => v.Codigo)
            .Should().Equal(["ALFA_VALOR", "ZETA_VALOR"], "pré-condição: ordenar pelo Codigo dá o oposto do oráculo de Ordem");

    /// <summary>
    /// Pré-condição pelos bytes canônicos REAIS do item, na forma que a produção emite: um mutante que
    /// ordenasse pelo conteúdo do item divergiria já em <c>descricao</c>, antes de alcançar <c>ordem</c>.
    /// </summary>
    internal static void ConferirQueOsBytesCanonicosOrdenamAoContrarioDaOrdem(ValorDominioDeclaradoCongelado zeta, ValorDominioDeclaradoCongelado alfa) =>
        ComparadorLexicograficoDeBytes.Instancia.Compare(BytesCanonicosDoItem(alfa), BytesCanonicosDoItem(zeta)).Should().BeLessThan(0,
            "pré-condição: os bytes canônicos reais do item (a chave `descricao`, que precede `ordem`) colocariam " +
            "ALFA_VALOR antes de ZETA_VALOR — o oposto do oráculo de Ordem — se a política fosse ordenar pelo " +
            "conteúdo do item em vez de por Ordem");

    private static byte[] BytesCanonicosDoItem(ValorDominioDeclaradoCongelado valor) =>
        PerfilCanonicoV1.Instancia.Serializar(new JsonObject
        {
            ["valorCodigo"] = valor.Codigo,
            ["descricao"] = valor.Descricao,
            ["ordem"] = valor.Ordem,
        });

    internal static Dictionary<string, MetadadoFatoCongelado> MetadadosDaCorRaca(ValorDominioDeclaradoCongelado alfa, ValorDominioDeclaradoCongelado zeta) =>
        new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA", Dominio: TipoDoDado.Opcao, Origem: OrigemFato.Declarado, Cardinalidade: QuantidadeDeValores.ValorUnico,
                PontoResolucao: "INSCRICAO", Binding: "CAMPO_FORMULARIO:COR_RACA",
                ValoresDominio: null, ValoresDominioDeclarados: [alfa, zeta]),
        };

    internal static AtendimentoEspecializado AtendimentoComIdentidadeDeOrigemInvertida() =>
        AtendimentoEspecializado.Criar(
            condicoes: CondicoesComOrigemInvertida(),
            recursos: RecursosComOrigemInvertida(),
            tiposDeficiencia: TiposDeDeficienciaComOrigemInvertida()).Value!;

    /// <summary>Em ordem de entrada: a de origem maior antes da de origem menor, e a condição PCD por último.</summary>
    private static CondicaoOfertada[] CondicoesComOrigemInvertida()
    {
        CondicaoOfertada comOrigemMenor = CondicaoOfertada.Criar(IdFixo(1), "COD_ZETA", "Zeta Nome");
        CondicaoOfertada comOrigemMaior = CondicaoOfertada.Criar(IdFixo(2), "COD_ALFA", "Alfa Nome");
        CondicaoOfertada pcd = CondicaoOfertada.Criar(IdFixo(9), AtendimentoEspecializado.CodigoCondicaoPcd, "Atendimento PCD");
        ConferirQueONomeOrdenaAoContrarioDaOrigem(static c => c.CondicaoNome, comOrigemMenor, comOrigemMaior);

        return [comOrigemMaior, comOrigemMenor, pcd];
    }

    private static RecursoOfertado[] RecursosComOrigemInvertida()
    {
        RecursoOfertado comOrigemMenor = RecursoOfertado.Criar(IdFixo(1), "ZETA_RECURSO", "Zeta Recurso");
        RecursoOfertado comOrigemMaior = RecursoOfertado.Criar(IdFixo(2), "ALFA_RECURSO", "Alfa Recurso");
        ConferirQueONomeOrdenaAoContrarioDaOrigem(static r => r.RecursoNome, comOrigemMenor, comOrigemMaior);

        return [comOrigemMaior, comOrigemMenor];
    }

    private static DeficienciaAtendida[] TiposDeDeficienciaComOrigemInvertida()
    {
        DeficienciaAtendida comOrigemMenor = DeficienciaAtendida.Criar(IdFixo(1), "ZETA_TIPO", "Zeta Tipo");
        DeficienciaAtendida comOrigemMaior = DeficienciaAtendida.Criar(IdFixo(2), "ALFA_TIPO", "Alfa Tipo");
        ConferirQueONomeOrdenaAoContrarioDaOrigem(static t => t.TipoDeficienciaNome, comOrigemMenor, comOrigemMaior);

        return [comOrigemMaior, comOrigemMenor];
    }

    internal static void ConferirQueONomeOrdenaAoContrarioDaOrigem<T>(Func<T, string> nome, T comOrigemMenor, T comOrigemMaior) =>
        new[] { comOrigemMenor, comOrigemMaior }.OrderBy(nome, StringComparer.Ordinal)
            .Should().Equal([comOrigemMaior, comOrigemMenor], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade");

    /// <summary>Cada array tem a sua expressão <c>OrderBy</c> própria: regredir só uma não seria pega pelo oráculo das outras.</summary>
    internal static void ConferirONomeNaOrdemDaOrigem(JsonObject atendimentoJson, string colecao, string campoNome, string identidadeDeOrigem, string[] esperado) =>
        atendimentoJson[colecao]!.AsArray().Select(n => n![campoNome]!.GetValue<string>()).Should().Equal(
            esperado,
            $"a identidade de origem ({identidadeDeOrigem}) governa a posição — o nome, em ordem alfabética, apontaria para o oposto");

    internal static DocumentoExigido ExigenciaDeTeste(Guid id, Guid faseId, Guid tipoDocumentoOrigemId, string tipoDocumentoNome) =>
        DocumentoExigido.Reidratar(
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar(tipoDocumentoNome == "Alfa Documento" ? "exigencia_alfa" : "exigencia_zeta").Value!,
            id,
            new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(tipoDocumentoOrigemId, "TIPO_COMUM", tipoDocumentoNome, "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(faseId, null, FinalidadeFormulario.Inscricao),
                new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
                new IndeferimentoDoDocumento(false, null, []),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!),
                null));

    internal static IReadOnlyList<string> NomesDasExigenciasNoEnvelope(FaseCronograma fase, params RegraDeComprovacao[] regrasSemGrupo)
    {
        ProcessoSeletivo processo = Montar(cronogramaFases: [fase], documentosExigidos: regrasSemGrupo);
        JsonArray exigenciasJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["documentosExigidos"]!["exigencias"]!.AsArray();

        return [.. exigenciasJson.Select(static e => e!["tipoDocumentoNome"]!.GetValue<string>())];
    }

    internal static FatoColetado FatoBooleanoPessoal(string codigo, int ordem, PredicadoDnf? exibicao) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .ComRotulo($"Rótulo {codigo[..1]}")
            .ExibidoQuando(exibicao)
            .Construir();

    private static bool DoCaso(string idCanonico) =>
        idCanonico.EndsWith("/A_FATO", StringComparison.Ordinal) || idCanonico.EndsWith("/B_FATO", StringComparison.Ordinal);

    private static readonly string[] OrdemCanonicaDosNos = ["CAMPO/B_FATO", "DADO/B_FATO", "CAMPO/A_FATO", "DADO/A_FATO"];

    internal static void ConferirAOrdemTopologica(JsonObject grafoJson)
    {
        // Só os nós do caso: o formulário de inscrição traz também os do conjunto básico.
        List<string> ordemTopologica = [.. grafoJson["ordemDeDependencia"]!.AsArray().Select(static n => n!.GetValue<string>()).Where(DoCaso)];
        ordemTopologica.Should().HaveCountGreaterThanOrEqualTo(2, "pré-condição: o caminho existe e tem nós de A_FATO e de B_FATO");
        List<string> ordenadosAlfabeticamente = [.. ordemTopologica.OrderBy(static r => r, StringComparer.Ordinal)];
        ordenadosAlfabeticamente.FindIndex(static r => r.Contains("A_FATO", StringComparison.Ordinal))
            .Should().BeLessThan(
                ordenadosAlfabeticamente.FindIndex(static r => r.Contains("B_FATO", StringComparison.Ordinal)),
                "pré-condição: reordenar os MESMOS rótulos alfabeticamente poria A_FATO antes de B_FATO — o oposto da ordem topológica " +
                "que o domínio calcula (produtor antes do consumidor)");

        ordemTopologica.Should().Equal(
            OrdemCanonicaDosNos,
            "a ordem topológica é CALCULADA por GrafoDependenciaConjunta (Kahn, produtor antes do consumidor) e apenas PRESERVADA " +
            "pela projeção — nunca recalculada nem reordenada alfabeticamente aqui");
    }

    internal static void ConferirOsNosDoGrafo(JsonObject grafoJson) =>
        grafoJson["itens"]!.AsArray().Select(static n => n!["identificador"]!.GetValue<string>()).Where(DoCaso).Should().Equal(
            OrdemCanonicaDosNos,
            "os nós saem na MESMA ordem canônica que GrafoDependenciaConjunta calcula (ordem de coleta efetiva, depois Classe/Codigo) — " +
            "não a ordem alfabética, que poria CAMPO/A_FATO primeiro");

    /// <summary>
    /// As origens das duas arestas de RESPOSTA_DO_CAMPO ("CAMPO/" + o código do fato), em ordem alfabética,
    /// começariam por A_FATO — o oposto da ordem canônica, que segue a ordem de coleta efetiva do nó de
    /// origem (B_FATO, Ordem 0, primeiro).
    /// </summary>
    internal static void ConferirAsArestasDoGrafo(JsonObject grafoJson) =>
        grafoJson["ligacoes"]!.AsArray()
            .Select(static a => (Tipo: a!["tipo"]!.GetValue<string>(), Origem: a["origem"]!.GetValue<string>(), Destino: a["destino"]!.GetValue<string>()))
            .Where(static a => DoCaso(a.Origem))
            .Should().Equal(
                [
                    ("RESPOSTA_DO_CAMPO", "CAMPO/B_FATO", "DADO/B_FATO"),
                    ("RESPOSTA_DO_CAMPO", "CAMPO/A_FATO", "DADO/A_FATO"),
                    ("EXIBICAO", "DADO/B_FATO", "CAMPO/A_FATO"),
                ],
                "as arestas saem ordenadas por (Tipo, Origem, Destino) canônicos — as duas de RESPOSTA_DO_CAMPO desempatam pela ordem de coleta " +
                "efetiva da origem (B_FATO antes de A_FATO), o oposto da ordem alfabética da pré-condição");

    /// <summary>Inserido fisicamente na ordem ERRADA (Zebu antes de Abaco): um canonicalizador que preservasse a ordem de entrada reproduziria a sequência errada.</summary>
    internal static ConfiguracaoDoFada FadaComMunicipiosDeMesmoCodigoIbgeForaDeOrdem() =>
        ConfiguracaoDoFada.Criar(
            Regra(RegraDoFada.Multiplicativo.Codigo(), 'd'), 1.20m, null, null,
            new BaseLegalCongelada(
                Guid.CreateVersion7(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Teste 1/2026", "Descrição de teste",
                [("1504208", "Zebu", "PA"), ("1504208", "Abaco", "PA")])).Value!;
}
