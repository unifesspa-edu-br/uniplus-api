namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>
/// O comprovante montado a partir dos insumos, o mesmo para o titular e para a unidade que o emite: o texto vem da versão fixada da inscrição e os valores, do próprio
/// comprovante, de modo que o documento emitido não muda com a retificação do edital. Nada daqui lê o que o certame serve hoje.
/// </summary>
internal static class ComprovanteDoTitular
{
    public static ComprovanteDaInscricaoDto Montar(InsumosDoComprovante insumos)
    {
        ArgumentNullException.ThrowIfNull(insumos);

        return new()
        {
            Candidato = CandidatoDoComprovante.De(insumos),
            Inscricao = MontarInscricao(insumos),
            Processo = MontarProcesso(insumos),
            EscolhasDeCurso = OpcoesDeCursoDoComprovante.Montar(insumos),
            Formulario = MontarFormulario(insumos),
        };
    }

    private static InscricaoNoComprovanteDto MontarInscricao(InsumosDoComprovante insumos) => new()
    {
        InscricaoId = insumos.Inscricao.Id,
        Numero = insumos.Posicao.Numero,
        Total = insumos.Posicao.Total,
        Vale = insumos.Posicao.Vale,
        NumeroAnterior = insumos.Posicao.NumeroAnterior,
        NumeroSeguinte = insumos.Posicao.NumeroSeguinte,
        Envio = EnvioDoComprovante.De(insumos),
        Cancelamento = insumos.Cancelamento,
        PrazoDaCorrecao = insumos.Prazo,
    };

    private static ProcessoNoComprovanteDto MontarProcesso(InsumosDoComprovante insumos) => new()
    {
        ProcessoSeletivoId = insumos.Inscricao.ProcessoSeletivoId,
        NomeDoProcesso = insumos.NomeDoProcesso,
        IdentificadorDoProcesso = insumos.Envelope.Configuracao.IdentificadorLegivel?.Valor,
        UnidadeAdministradora = insumos.NomeDaUnidadeAdministradora,
    };

    private static FormularioNoComprovanteDto MontarFormulario(InsumosDoComprovante insumos)
    {
        FatosDaVersao fatos = FatosDaVersao.De(insumos.Envelope);
        return new()
        {
            Secoes = ItensDoComprovante.Secoes(insumos, fatos),
            Derivados = ItensDoComprovante.Derivados(insumos, fatos),
            TermosAceitos = TermosEDocumentosDoComprovante.Termos(insumos),
            Documentos = TermosEDocumentosDoComprovante.Documentos(insumos, fatos),
        };
    }
}
