namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;

/// <summary>O processo publicado com as modalidades da Lei 12.711 e as respostas que decidem a origem escolar e as cotas.</summary>
internal static class ProcessoComMatrizDaLeiDeTeste
{
    /// <summary>
    /// Um processo publicado com as dez modalidades da Lei 12.711 ofertadas, cada uma com a regra de
    /// aplicabilidade da matriz da lei (UNI-REQ-0076), as opções de concorrência e a origem escolar
    /// perguntadas, e o egresso de escola pública derivado da origem escolar.
    /// </summary>
    internal static ProcessoSeletivo ProcessoComAMatrizNormativa(string nome)
    {
        FatoColetado Booleano(string codigo, int ordem) => FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "SENSIVEL",
            etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        FatoColetado Selecao(string codigo, int ordem) => FatoColetado.Criar(
            codigo, ordem, $"Rótulo de {codigo}", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nome,
            camposAdicionais:
            [
                Selecao(OrigemEscolar.FatoFormaDeConclusao, 1), Selecao(OrigemEscolar.FatoOndeCursou, 2),
                Booleano("CONCORRER_PCD", 3), Booleano("CONCORRER_EP", 4), Booleano("CONCORRER_PPI", 5),
                Booleano("CONCORRER_Q", 6), Booleano("CONCORRER_RENDA", 7),
            ],
            modalidades: [.. RegrasDaLei.Select(static r => ModalidadeDaLei(r.Key, r.Value))]);
        processo.DefinirRegrasDerivacao(
        [
            Configurada(DerivacaoDoFato.CriarBooleana(
                OrigemEscolar.FatoEgresso, OrigemEscolar.RegrasDoEgresso, OrigemEscolar.DependenciasDoEgresso).Value!),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>As opções da origem escolar que a publicação congela para as duas perguntas.</summary>
    internal static Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> OpcoesDaOrigemEscolar() => new(StringComparer.Ordinal)
    {
        [OrigemEscolar.FatoFormaDeConclusao] = [.. new[] { OrigemEscolar.Regular, OrigemEscolar.Eja, OrigemEscolar.Encceja }
            .Select(static (c, i) => new ValorDominioDeclaradoCongelado(c, c, i))],
        [OrigemEscolar.FatoOndeCursou] = [.. OrigemEscolar.OndeCursouSemCertificacao.Append(OrigemEscolar.ConcluiuPorCertificacao)
            .Select(static (c, i) => new ValorDominioDeclaradoCongelado(c, c, i))],
    };

    /// <summary>
    /// As regras da matriz da lei sobre a opção de cada dimensão: optar pelas cotas é ser egresso de
    /// escola pública e optar pelas vagas de escola pública, a cota de renda acrescenta a renda, e a
    /// reserva de pessoa com deficiência vale para quem não optou pelas cotas.
    /// </summary>
    private static readonly Dictionary<string, PredicadoDnf> RegrasDaLei = new(StringComparer.Ordinal)
    {
        ["AC"] = PredicadoDnf.SemClausula,
        ["AC_PCD"] = Clausulas(
            [(OpcaoPelasCotasDaLei.Fato, false), ("CONCORRER_PCD", true), (OrigemEscolar.FatoEgresso, false)],
            [(OpcaoPelasCotasDaLei.Fato, false), ("CONCORRER_PCD", true), ("CONCORRER_EP", false)]),
        ["LI_EP"] = Clausulas([.. OptouPelasCotas]),
        ["LB_EP"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_RENDA", true)]),
        ["LI_PPI"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_PPI", true)]),
        ["LB_PPI"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_PPI", true), ("CONCORRER_RENDA", true)]),
        ["LI_Q"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_Q", true)]),
        ["LB_Q"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_Q", true), ("CONCORRER_RENDA", true)]),
        ["LI_PCD"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_PCD", true)]),
        ["LB_PCD"] = Clausulas([.. OptouPelasCotas, ("CONCORRER_PCD", true), ("CONCORRER_RENDA", true)]),
    };

    private static (string, bool)[] OptouPelasCotas => [(OrigemEscolar.FatoEgresso, true), ("CONCORRER_EP", true)];

    /// <summary>
    /// A modalidade ofertada, num quadro institucional fixo: a ampla concorrência e as demais retirando
    /// vagas dela. O que se prova aqui é a derivação das modalidades, e não o cálculo do quadro da lei
    /// nem a cascata das cotas, então só a reserva PcD tem a natureza de ação afirmativa, sem cota da
    /// lei ofertada.
    /// </summary>
    private static ModalidadeSelecionada ModalidadeDaLei(string codigo, PredicadoDnf regra) => codigo == "AC"
        ? ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, "Ampla concorrência", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, regra, null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 22).Value!
        : codigo == "AC_PCD"
            ? ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), codigo, $"Modalidade {codigo}", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
                "AC", RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null, regra, null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 2).Value!
            : ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), codigo, $"Modalidade {codigo}", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
                "AC", RegraRemanejamentoModalidade.Nenhuma, null, null, null, regra, null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 2).Value!;

    /// <summary>A derivação na forma que o processo grava: cada cláusula do predicado vira um grupo de condições.</summary>
    private static ConfiguracaoDerivacaoFato Configurada(DerivacaoDoFato derivacao) =>
        ConfiguracaoDerivacaoFato.Criar(derivacao.CodigoFato,
        [
            .. derivacao.Regras.Select((regra, ordem) => RegraDerivacaoConfigurada.Criar(
                ordem,
                regra.Contribui,
                regra.EhAncora
                    ? null
                    : [.. regra.Quando.Clausulas.SelectMany((clausula, indice) => clausula.Condicoes.Select(c =>
                        CondicaoRegraDerivacao.Criar(indice + 1, c.Fato, c.Operador, c.Valor).Value!))]).Value!),
        ]).Value!;

    internal static Dictionary<string, JsonElement> RespostasDaOrigemEDasCotas(string? ondeCursou, bool ep, bool ppi, bool renda)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false),
            ["CONCORRER_EP"] = JsonSerializer.SerializeToElement(ep),
            ["CONCORRER_PPI"] = JsonSerializer.SerializeToElement(ppi),
            ["CONCORRER_Q"] = JsonSerializer.SerializeToElement(false),
            ["CONCORRER_RENDA"] = JsonSerializer.SerializeToElement(renda),
        };
        if (ondeCursou is not null)
        {
            respostas[OrigemEscolar.FatoFormaDeConclusao] = JsonSerializer.SerializeToElement(OrigemEscolar.Regular);
            respostas[OrigemEscolar.FatoOndeCursou] = JsonSerializer.SerializeToElement(ondeCursou);
        }

        return respostas;
    }
}
