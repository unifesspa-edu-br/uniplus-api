namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O tipo de banca que a seleção pode prever em uma fase ou em uma etapa (UNI-REQ-0139). Vocabulário fechado: o cadastro de
/// tipos de banca guarda um registro por tipo, e o código e o rótulo do tipo são fixos, ao contrário do nome do cadastro, que
/// pode ser editado.
/// </summary>
[DescricaoDoContrato("O tipo de banca que a seleção pode prever em uma fase ou em uma etapa. O código e o rótulo do tipo são fixos; o nome do cadastro de tipos de banca pode ser editado.")]
public enum TipoDeBanca
{
    [CodigoDeContrato("BANCA_ANALISE_DOCUMENTAL", "Banca de análise documental")]
    AnaliseDocumental = 1,

    [CodigoDeContrato("BANCA_ENTREVISTA", "Banca de entrevista")]
    Entrevista = 2,

    [CodigoDeContrato("BANCA_CORRECAO_REDACOES", "Banca de correção de redações")]
    CorrecaoDeRedacoes = 3,

    [CodigoDeContrato("BANCA_ANALISE_RECURSOS", "Banca de análise de recursos")]
    AnaliseDeRecursos = 4,

    [CodigoDeContrato("BANCA_HETEROIDENTIFICACAO", "Banca de heteroidentificação")]
    Heteroidentificacao = 5,

    [CodigoDeContrato("BANCA_BIOPSICOSSOCIAL", "Banca de avaliação biopsicossocial")]
    AvaliacaoBiopsicossocial = 6,

    [CodigoDeContrato("BANCA_HOMOLOGACAO_INSCRICOES", "Banca de homologação das inscrições")]
    HomologacaoDasInscricoes = 7,

    [CodigoDeContrato("BANCA_IMPORTACAO_NOTAS_INEP", "Banca de importação das notas do INEP")]
    ImportacaoDasNotasDoInep = 8,

    [CodigoDeContrato("BANCA_IMPORTACAO_CANDIDATOS", "Banca de importação de candidatos")]
    ImportacaoDeCandidatos = 9,

    [CodigoDeContrato("BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA", "Banca de análise de recursos de 2ª instância")]
    AnaliseDeRecursosDeSegundaInstancia = 10,
}
