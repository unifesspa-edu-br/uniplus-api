namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>
/// O comprovante do titular montado a partir dos insumos: o texto vem da versão fixada da inscrição e os valores, do próprio
/// comprovante, de modo que o documento emitido não muda com a retificação do edital. Nada daqui lê o que o certame serve hoje.
/// </summary>
internal static class ComprovanteDoTitular
{
    public static ComprovanteDaInscricaoDto Montar(InsumosDoComprovante insumos)
    {
        ArgumentNullException.ThrowIfNull(insumos);

        FatosDaVersao fatos = FatosDaVersao.De(insumos.Envelope);

        return new()
        {
            InscricaoId = insumos.Inscricao.Id,
            Numero = insumos.Posicao.Numero,
            Total = insumos.Posicao.Total,
            Vale = insumos.Posicao.Vale,
            NumeroAnterior = insumos.Posicao.NumeroAnterior,
            NumeroSeguinte = insumos.Posicao.NumeroSeguinte,
            ProcessoSeletivoId = insumos.Inscricao.ProcessoSeletivoId,
            NomeDoProcesso = insumos.NomeDoProcesso,
            IdentificadorDoProcesso = insumos.Envelope.Grafo.IdentificadorLegivel?.Valor,
            UnidadeAdministradora = insumos.NomeDaUnidadeAdministradora,
            Envio = EnvioDoComprovante.De(insumos),
            Candidato = CandidatoDoComprovante.De(insumos),
            PrazoDaCorrecao = insumos.Prazo,
            NumeroDeOpcoesDeCurso = OpcoesDeCursoDoComprovante.NumeroDeOpcoes(insumos),
            OpcoesDeCurso = OpcoesDeCursoDoComprovante.De(insumos),
            Secoes = ItensDoComprovante.Secoes(insumos, fatos),
            Derivados = ItensDoComprovante.Derivados(insumos, fatos),
            TermosAceitos = TermosEDocumentosDoComprovante.Termos(insumos),
            Documentos = TermosEDocumentosDoComprovante.Documentos(insumos, fatos),
        };
    }
}
