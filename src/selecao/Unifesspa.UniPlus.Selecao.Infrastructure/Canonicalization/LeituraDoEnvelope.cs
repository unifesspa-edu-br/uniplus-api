namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê, na ordem do envelope, cada bloco pela classe de leitura do seu assunto e entrega os blocos
/// ainda sem conferir a coerência entre eles. O primeiro desvio fica no <see cref="LeitorEnvelope"/>.
/// </summary>
internal static class LeituraDoEnvelope
{
    /// <summary>
    /// Lê todos os blocos do envelope.
    /// </summary>
    /// <param name="leitor">O leitor que acumula o primeiro erro.</param>
    /// <param name="payload">O objeto canônico já parseado.</param>
    /// <param name="temRetificacao">Se o envelope carrega o bloco de retificação.</param>
    /// <returns>Os blocos lidos, ou nulo quando a leitura falhou.</returns>
    internal static BlocosDoEnvelope? Ler(LeitorEnvelope leitor, JsonObject payload, bool temRetificacao)
    {
        BlocosDoEnvelope blocos = new();
        LerEditalEClassificacao(leitor, payload, blocos);
        LerExigenciasEFormularios(leitor, payload, blocos);
        blocos.Retificacao = temRetificacao ? LeituraDaRetificacao.LerRetificacao(leitor, payload) : null;
        if (leitor.Falhou)
        {
            return null;
        }

        Dictionary<Guid, DocumentoExigido> exigenciasPorId = IndexarExigenciasPorId(leitor, blocos.DocumentosExigidos);
        if (leitor.Falhou)
        {
            return null;
        }

        blocos.Raizes = LeituraDaArvoreDeSatisfacao.LerArvoreSatisfacao(leitor, payload, exigenciasPorId);
        if (leitor.Falhou)
        {
            return null;
        }

        LerFatosEDerivacao(leitor, payload, blocos);
        return leitor.Falhou ? null : blocos;
    }

    private static void LerEditalEClassificacao(LeitorEnvelope leitor, JsonObject payload, BlocosDoEnvelope blocos)
    {
        LeituraDoEdital.LerTipoProcesso(leitor, payload);

        blocos.Dados = LeituraDoEdital.LerDadosEdital(leitor, payload, out string hashDocumento);
        blocos.HashDocumento = hashDocumento;
        blocos.Etapas = LeituraDasEtapas.LerEtapas(leitor, payload);
        blocos.Distribuicao = LeituraDasVagas.LerDistribuicao(leitor, payload);
        blocos.Atendimento = LeituraDoBonusEAtendimento.LerAtendimento(leitor, payload);
        blocos.Bonus = LeituraDoBonusEAtendimento.LerBonusRegional(leitor, payload);
        blocos.Cascata = LeituraDaCascata.LerCascataRemanejamento(leitor, payload);
        blocos.Desempate = LeituraDoDesempate.LerCriteriosDesempate(leitor, payload);
        blocos.Classificacao = LeituraDaClassificacao.LerClassificacao(leitor, payload);
        blocos.CronogramaFases = LeituraDoCronograma.LerCronogramaFases(leitor, payload, comId: true);
        LeituraDoEdital.LerIdentidadesUnidade(leitor, payload);
    }

    private static void LerExigenciasEFormularios(LeitorEnvelope leitor, JsonObject payload, BlocosDoEnvelope blocos)
    {
        (blocos.Conformidade, blocos.DocumentosExigidos, blocos.ReferenciaTemporalFatos, blocos.MetadadosFatosCongelados) =
            LeituraDosDocumentosExigidos.LerDocumentosExigidos(leitor, payload);
        (blocos.Formularios, blocos.TermosExigidos) = LeituraDosFormularios.LerFormularios(leitor, payload);
        blocos.Divulgacao = LeituraDaDivulgacaoETaxa.LerDivulgacao(leitor, payload);
        blocos.TaxaInscricao = LeituraDaDivulgacaoETaxa.LerTaxaInscricao(leitor, payload);
        (blocos.Localidade, blocos.FusoHorario) = LeituraDoCalendario.LerLocalidade(leitor, payload);
        blocos.NumeroDeOpcoesDeCurso = LeituraDasVagas.LerOpcoesDeCurso(leitor, payload);
        blocos.AlgoritmoContagemPrazo = LeituraDoCalendario.LerAlgoritmoContagemPrazo(leitor, payload);
        blocos.CalendarioDiasUteis = LeituraDoCalendario.LerCalendarioDiasUteis(leitor, payload, blocos.CronogramaFases, blocos.Etapas);
        blocos.IdentificadorLegivel = LeituraDoEdital.LerIdentificadorLegivel(leitor, payload);
    }

    private static void LerFatosEDerivacao(LeitorEnvelope leitor, JsonObject payload, BlocosDoEnvelope blocos)
    {
        (IReadOnlyList<FatoColetado> fatos, Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) =
            LeituraDosFatos.LerFatosColetados(leitor, payload);
        blocos.FatosColetados = fatos;
        blocos.ValoresSelecionaveis = valores;
        blocos.GruposColetados = leitor.Falhou
            ? []
            : LeituraDosGrupos.LerGruposColetados(leitor, payload, valores, blocos.AgregadosDosGrupos);
        blocos.RegrasDerivacao = LeituraDaDerivacao.LerRegrasDerivacao(leitor, payload);
        blocos.VersaoInterpretador = leitor.TextoNaoVazio(payload, "versaoInterpretador", "$");
        blocos.ApresentacaoDosPressupostos = LeituraDoEdital.LerApresentacaoDosPressupostos(leitor, payload);
        blocos.TiposDeAtoCongelados = LeituraDosTiposDeAto.LerTiposDeAto(leitor, payload, blocos.CronogramaFases, blocos.Etapas);
        blocos.FatosDerivadosCongelados = LeituraDosFatosDerivados.LerFatosDerivados(leitor, payload);
    }

    /// <summary>
    /// O bloco de exigências nunca repete <c>exigenciaId</c> quando produzido por um encoder real, mas
    /// um envelope adulterado poderia. Sem esta checagem, o dicionário lançaria exceção (500) em vez
    /// de recusar o envelope como malformado.
    /// </summary>
    private static Dictionary<Guid, DocumentoExigido> IndexarExigenciasPorId(
        LeitorEnvelope leitor, IReadOnlyList<DocumentoExigido> documentosExigidos)
    {
        List<Guid> duplicados = [.. documentosExigidos
            .GroupBy(static d => d.Id)
            .Where(static grupo => grupo.Count() > 1)
            .Select(static grupo => grupo.Key)];

        return duplicados.Count > 0
            ? leitor.Recusar<Dictionary<Guid, DocumentoExigido>>(
                $"'documentosExigidos.exigencias': o exigenciaId '{duplicados[0]}' aparece mais de uma vez.") ?? []
            : documentosExigidos.ToDictionary(static d => d.Id);
    }
}
