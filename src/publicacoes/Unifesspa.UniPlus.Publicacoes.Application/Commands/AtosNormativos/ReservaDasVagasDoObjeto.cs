namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Reserva, para cada entidade vinculada, a vaga do objeto em nome da linhagem do ato,
/// quando o tipo admite um único ato vivo por objeto (ADR-0107).
/// </summary>
/// <remarks>
/// São duas verificações, e cada uma cobre o que a outra não vê:
/// <list type="number">
///   <item>o <b>histórico de atos</b>: um ato do mesmo tipo já vinculado ao objeto, fora desta
///     linhagem, colide. Enxerga inclusive o ato publicado quando o tipo ainda não era único
///     por objeto (o catálogo é editável) e que, por isso, não reservou vaga nenhuma;</item>
///   <item>a <b>vaga</b>: o índice único que o banco trava, e que fecha a corrida entre duas
///     transações concorrentes, invisível a qualquer consulta.</item>
/// </list>
/// A vaga já ocupada <i>pela própria linhagem</i> não é reservada de novo: a segunda versão de
/// um ato vivo é uma retificação, e a vaga é da linhagem, não do ato.
/// </remarks>
public sealed class ReservaDasVagasDoObjeto(IAtoNormativoRepository atos)
{
    /// <summary>
    /// Devolve o <see cref="DomainError"/> da primeira colisão, ou <see langword="null"/> quando
    /// o tipo não é único por objeto, o ato não vincula entidade alguma, ou todas as vagas foram
    /// reservadas.
    /// </summary>
    /// <remarks>
    /// Duas fases, e a ordem importa: nada é reservado enquanto houver um vínculo por examinar.
    /// Reservar durante o exame deixaria, num ato com dois objetos em que só o segundo conflita,
    /// a vaga do primeiro pendurada na unidade de trabalho, e a recusa do ato inteiro não a
    /// desfaria por si só. O próprio ato ainda não está gravado, mas já integra a linhagem:
    /// incluí-lo evita que ele se veja como conflito de si mesmo.
    /// </remarks>
    public async Task<DomainError?> ReservarAsync(
        AtoNormativo ato,
        IReadOnlyList<Guid> idsDaPropriaCadeia,
        Guid raizDaLinhagem,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ato);

        if (!ato.UnicoPorObjeto || ato.Vinculos.Count == 0)
        {
            return null;
        }

        return await ExaminarEReservarAsync(ato, idsDaPropriaCadeia, raizDaLinhagem, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DomainError?> ExaminarEReservarAsync(
        AtoNormativo ato,
        IReadOnlyList<Guid> idsDaPropriaCadeia,
        Guid raizDaLinhagem,
        CancellationToken cancellationToken)
    {
        Guid[] daLinhagem = [.. idsDaPropriaCadeia, ato.Id];
        List<VagaDeLinhagem> aReservar = [];
        foreach (VinculoAtoEntidade vinculo in ato.Vinculos)
        {
            DomainError? recusa = await ExaminarAsync(ato, vinculo, daLinhagem, raizDaLinhagem, aReservar, cancellationToken)
                .ConfigureAwait(false);
            if (recusa is not null)
            {
                return recusa;
            }
        }

        await ReservarTodasAsync(aReservar, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<DomainError?> ExaminarAsync(
        AtoNormativo ato,
        VinculoAtoEntidade vinculo,
        Guid[] daLinhagem,
        Guid raizDaLinhagem,
        List<VagaDeLinhagem> aReservar,
        CancellationToken cancellationToken)
    {
        AtoNormativo? conflitante = await atos
            .ObterAtoConflitanteNoObjetoAsync(
                vinculo.EntidadeTipo, vinculo.EntidadeId, ato.TipoCodigo, daLinhagem, cancellationToken)
            .ConfigureAwait(false);
        if (conflitante is not null)
        {
            return Ocupada(ObjetoJaTemAtoVivoMensagem(vinculo, ato.TipoCodigo, conflitante));
        }

        VagaDeLinhagem? vaga = await atos
            .ObterVagaDoObjetoAsync(vinculo.EntidadeTipo, vinculo.EntidadeId, ato.TipoCodigo, cancellationToken)
            .ConfigureAwait(false);
        return await ExaminarVagaAsync(ato, vinculo, vaga, raizDaLinhagem, aReservar, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A vaga existe: só a própria linhagem pode reencontrá-la. A consulta ao histórico já teria
    /// barrado qualquer outra, e esta é a rede de segurança.
    /// </summary>
    private async Task<DomainError?> ExaminarVagaAsync(
        AtoNormativo ato,
        VinculoAtoEntidade vinculo,
        VagaDeLinhagem? vaga,
        Guid raizDaLinhagem,
        List<VagaDeLinhagem> aReservar,
        CancellationToken cancellationToken)
    {
        if (vaga is null)
        {
            aReservar.Add(VagaDeLinhagem.Criar(ato, vinculo, raizDaLinhagem));
            return null;
        }

        return vaga.RaizId == raizDaLinhagem
            ? null
            : Ocupada(await MensagemDaVagaDeOutraLinhagemAsync(vinculo, ato.TipoCodigo, vaga, cancellationToken).ConfigureAwait(false));
    }

    private async Task ReservarTodasAsync(
        List<VagaDeLinhagem> aReservar, CancellationToken cancellationToken)
    {
        foreach (VagaDeLinhagem vaga in aReservar)
        {
            await atos.ReservarVagaAsync(vaga, cancellationToken).ConfigureAwait(false);
        }
    }

    private static DomainError Ocupada(string mensagem) =>
        new(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo, mensagem);

    private static string ObjetoJaTemAtoVivoMensagem(
        VinculoAtoEntidade vinculo, string tipoCodigo, AtoNormativo conflitante) =>
        $"O tipo de ato '{tipoCodigo}' admite um único ato vivo por objeto, e o objeto vinculado ('{vinculo.EntidadeTipo}') "
        + $"já é tratado pelo ato {NumeracaoDoAto.Descrever(conflitante)}. "
        + "Para publicar outra versão, retifique a linhagem existente.";

    // A raiz da linhagem que ocupa a vaga é um ato registrado; só a leitura que falha em achá-lo deixa a recusa sem nomeá-lo.
    private async Task<string> MensagemDaVagaDeOutraLinhagemAsync(
        VinculoAtoEntidade vinculo, string tipoCodigo, VagaDeLinhagem vaga, CancellationToken cancellationToken)
    {
        AtoNormativo? raiz = await atos.ObterPorIdParaLeituraAsync(vaga.RaizId, cancellationToken).ConfigureAwait(false);
        string ocupante = raiz is null ? "por outra linhagem de atos" : $"pela linhagem do ato {NumeracaoDoAto.Descrever(raiz)}";

        return $"O tipo de ato '{tipoCodigo}' admite um único ato vivo por objeto, e o objeto vinculado ('{vinculo.EntidadeTipo}') "
            + $"já é tratado {ocupante}. Para publicar outra versão, retifique essa linhagem.";
    }
}
