namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

internal static class MontagemDoCongelamento
{
    /// <summary>
    /// Lê o calendário vigente, canonicaliza a configuração e monta o pedido que o agregado congela.
    /// A leitura do calendário é uma só por operação: a mesma resposta alimenta o portão da raiz e o
    /// bloco congelado do envelope, e ler duas vezes abriria a janela em que o dataset muda entre
    /// validar e congelar. O dataset incoerente não aborta aqui, porque só importa para o processo
    /// cuja contagem distingue dia útil, e quem decide isso é o portão da raiz.
    /// </summary>
    /// <param name="entrada">A entrada do canonicalizador, sem o calendário, que esta montagem acrescenta.</param>
    internal static async Task<PedidoDeCongelamento> MontarAsync(
        ICalendarioVigenteReader calendarios,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        TimeProvider timeProvider,
        EntradaCanonicalizacao entrada,
        TimeZoneInfo fusoInstitucional,
        string atorUsuarioSub,
        FatosDeModalidade fatosDeModalidade,
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos,
        CancellationToken cancellationToken)
    {
        CalendarioVigenteView? calendarioVigente = await calendarios
            .ObterVigenteAsync(cancellationToken)
            .ConfigureAwait(false);
        Result<CalendarioDiasUteisCongelado?> calendario = LeituraDoCalendarioVigente.Traduzir(calendarioVigente);

        ContextoDeContagemDePrazos contexto = new(
            calendario.IsSuccess ? calendario.Value : null,
            FalhaDoCalendarioVigente: calendario.IsFailure ? calendario.Error : null,
            FusoInstitucional: fusoInstitucional);

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(entrada with { CalendarioDiasUteis = contexto.CalendarioVigente });

        return new PedidoDeCongelamento
        {
            Dados = entrada.Dados,
            Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, entrada.HashDocumento),
            AtorUsuarioSub = atorUsuarioSub,
            Clock = timeProvider,
            Contexto = contexto,
            FatosDeModalidade = fatosDeModalidade,
            TiposDeAtoUnicos = tiposDeAtoUnicos,
        };
    }
}
