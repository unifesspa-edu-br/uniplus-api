namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Domain.ValueObjects;

/// <summary>Configuração da reconciliação da verificação dos arquivos; opcional, com padrões.</summary>
public sealed class ReconciliacaoDaVerificacaoOptions
{
    public const string SectionName = "Selecao:ReconciliacaoDaVerificacao";

    public static readonly TimeSpan PrazoMinimo = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Quanto o arquivo espera em verificação, desde a primeira publicação, antes de ser republicado. Precisa passar do
    /// tempo de fila no pico, ou o pedido que ainda está na fila seria republicado a cada execução.
    /// </summary>
    public TimeSpan Prazo { get; set; } = PoliticaDeRepublicacao.Padrao.Base;

    /// <summary>O prazo dobra a cada publicação até este teto, para a fila não acumular duplicados com o verificador fora do ar.</summary>
    public TimeSpan PrazoMaximo { get; set; } = PoliticaDeRepublicacao.Padrao.Maximo;

    public PoliticaDeRepublicacao Politica => new(Prazo, PrazoMaximo);

    public TimeSpan Intervalo { get; set; } = TimeSpan.FromMinutes(1);

    public int TamanhoDoLote { get; set; } = 100;
}
