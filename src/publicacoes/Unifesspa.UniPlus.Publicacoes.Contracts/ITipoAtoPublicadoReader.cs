namespace Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Leitor cross-módulo do catálogo de tipos de ato (ADR-0056). Deixa o domínio que vai
/// publicar conferir, ANTES de escrever, o tipo que o operador declarou.
/// </summary>
/// <remarks>
/// O registro do ato acontece depois, por mensagem durável (ADR-0108). Sem esta leitura, um
/// tipo inexistente — ou um tipo que não congela configuração declarado numa publicação, que
/// é justamente um congelamento — só seria recusado no consumo da fila: o Edital já estaria
/// publicado, o cliente já teria recebido 204, e a recusa viraria dead letter. O que o
/// catálogo pode recusar tem de virar 422 na hora.
/// </remarks>
public interface ITipoAtoPublicadoReader
{
    /// <summary>
    /// Versão vigente do tipo na <paramref name="dataPublicacao"/>, ou
    /// <see langword="null"/> quando o catálogo não tem versão vigente do código nessa data
    /// — a mesma resolução que Publicações faz ao registrar.
    /// </summary>
    Task<TipoAtoPublicadoView?> ObterVigenteAsync(
        string codigo,
        DateOnly dataPublicacao,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// O que o catálogo diz sobre um tipo de ato. São <b>dados</b> — jamais ramos de
/// comportamento: nenhum código compara <see cref="Codigo"/> com um literal (ADR-0103).
/// </summary>
public sealed record TipoAtoPublicadoView(
    string Codigo,
    string Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel);

/// <summary>
/// Leitor da vaga que uma linhagem de atos reserva sobre um objeto (ADR-0107).
/// </summary>
/// <remarks>
/// Um tipo <c>unico_por_objeto</c> admite uma única linhagem viva por objeto, e a vaga é
/// monotônica: ocupada, nunca se libera. Sem consultar isto antes de publicar, um certame
/// cuja vaga já esteja tomada — por um ato registrado pelo endpoint administrativo, por
/// exemplo — seria publicado com 204 e o registro do ato morreria na dead letter, deixando
/// o edital publicado sem ato.
/// </remarks>
public interface IVagaDeLinhagemReader
{
    /// <summary>
    /// <see langword="true"/> quando o objeto já tem, para este tipo, um ato de linhagem
    /// DIFERENTE da informada — isto é, quando a vaga está tomada por outro.
    /// </summary>
    /// <remarks>
    /// A pergunta é feita sobre o HISTÓRICO de atos do objeto, e não apenas sobre a tabela
    /// de vagas, porque <c>unico_por_objeto</c> é editável: um ato registrado enquanto o tipo
    /// ainda não era único não reservou vaga alguma, e mesmo assim conflita se o tipo passar
    /// a ser único depois. É exatamente a mesma checagem que o registro faz — se divergissem,
    /// a conferência aprovaria o que o registro recusaria, e a recusa voltaria à dead letter.
    /// <para>
    /// <paramref name="idsDaPropriaLinhagem"/> são os atos que já pertencem à linhagem de
    /// quem pergunta: uma retificação não disputa a vaga que a sua própria cadeia ocupa.
    /// Vazio na abertura, em que não há linhagem ainda.
    /// </para>
    /// </remarks>
    Task<bool> ObjetoJaTemAtoDeOutraLinhagemAsync(
        string entidadeTipo,
        Guid entidadeId,
        string tipoCodigo,
        IReadOnlyCollection<Guid> idsDaPropriaLinhagem,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <see langword="true"/> quando o ato já foi retificado — a cadeia é linear, e um ato é
    /// emendado no máximo uma vez (ADR-0103).
    /// </summary>
    /// <remarks>
    /// A retificação pode ter sido registrada por fora, pelo endpoint administrativo de
    /// Publicações. Sem esta conferência, Seleção emendaria um ato já emendado: o registro
    /// recusaria com <c>RaizJaRetificada</c> e a retificação ficaria publicada sem ato.
    /// </remarks>
    Task<bool> AtoJaFoiRetificadoAsync(Guid atoId, CancellationToken cancellationToken = default);
}
