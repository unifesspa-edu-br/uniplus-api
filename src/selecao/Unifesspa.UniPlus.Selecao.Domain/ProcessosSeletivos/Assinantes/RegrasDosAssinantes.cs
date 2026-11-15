namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As recusas da lista de assinantes de uma fase, as mesmas ao configurar e ao emitir. Cada posição
/// recebe no máximo uma recusa, a primeira na ordem em que orienta: a pessoa repetida sai primeiro, a
/// desconhecida em seguida; inativa antes de sem aceite, porque quem está inativo não aceita nada;
/// a autoridade vem por último, porque só faz sentido para quem pode assinar. A mensagem cita a
/// posição, nunca o código nem o nome da pessoa.
/// </summary>
internal static class RegrasDosAssinantes
{
    internal static Result Conferir(
        IReadOnlyList<string> codigos,
        Guid? unidadeDaFase,
        IReadOnlyDictionary<string, PessoaDoCadastroDeAssinantes> cadastro,
        EstruturaDasUnidadesAtivas estrutura,
        string caminhoDaLista)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        ArgumentNullException.ThrowIfNull(cadastro);
        ArgumentNullException.ThrowIfNull(estrutura);

        List<FieldError> recusas = [.. Enumerable.Range(0, codigos.Count)
            .Select(indice => RecusaDaPosicao(indice, codigos, unidadeDaFase, cadastro, estrutura, caminhoDaLista))
            .OfType<FieldError>()];

        return Resumir(recusas);
    }

    // O ciclo na hierarquia não é recusa de uma posição: impede a conferência inteira e falha sozinho.
    private static Result Resumir(List<FieldError> recusas) =>
        recusas.Find(static recusa => recusa.Error.Code == AssinantesErrorCodes.HierarquiaComCiclo) is { } ciclo
            ? Result.Failure(ciclo.Error)
            : recusas.Count == 0 ? Result.Success() : Result.ValidationFailure(recusas);

    private static FieldError? RecusaDaPosicao(
        int indice,
        IReadOnlyList<string> codigos,
        Guid? unidadeDaFase,
        IReadOnlyDictionary<string, PessoaDoCadastroDeAssinantes> cadastro,
        EstruturaDasUnidadesAtivas estrutura,
        string caminhoDaLista)
    {
        bool repetido = codigos.Take(indice).Contains(codigos[indice], StringComparer.Ordinal);
        cadastro.TryGetValue(codigos[indice], out PessoaDoCadastroDeAssinantes? pessoa);

        DomainError? recusa = RecusaDaPessoa(indice, repetido, pessoa)
            ?? RecusaDaAutoridade(indice, pessoa, unidadeDaFase, estrutura);

        return recusa is null ? null : new FieldError($"{caminhoDaLista}/{indice}", recusa);
    }

    private static DomainError? RecusaDaPessoa(int indice, bool repetido, PessoaDoCadastroDeAssinantes? pessoa)
    {
        int posicao = indice + 1;

        return (repetido, pessoa) switch
        {
            (true, _) => new DomainError(
                AssinantesErrorCodes.Repetido, $"A posição {posicao} repete uma pessoa já listada nesta fase."),
            (_, null) => new DomainError(
                AssinantesErrorCodes.NaoCadastrado, $"A pessoa da posição {posicao} não consta do cadastro de assinantes."),
            (_, { Ativa: false }) => new DomainError(
                AssinantesErrorCodes.Inativo, $"A pessoa da posição {posicao} está inativa no cadastro de assinantes."),
            (_, { AceiteVigente: false }) => new DomainError(
                AssinantesErrorCodes.SemAceiteVigente, $"A pessoa da posição {posicao} não tem o aceite do papel de assinante em vigor."),
            _ => null,
        };
    }

    // Fase sem unidade só tem a falta da unidade a apontar; a autoridade não se confere contra ninguém.
    private static DomainError? RecusaDaAutoridade(
        int indice, PessoaDoCadastroDeAssinantes? pessoa, Guid? unidadeDaFase, EstruturaDasUnidadesAtivas estrutura)
    {
        if (pessoa is null || unidadeDaFase is not { } unidade)
        {
            return null;
        }

        return AutoridadeParaAssinar.Avaliar(pessoa.UnidadeId, unidade, estrutura) switch
        {
            ResultadoDaAutoridade.Autorizada => null,
            ResultadoDaAutoridade.HierarquiaComCiclo => new DomainError(
                AssinantesErrorCodes.HierarquiaComCiclo, "A hierarquia das unidades forma um ciclo; a autoridade para assinar não pode ser conferida."),
            _ => new DomainError(
                AssinantesErrorCodes.SemAutoridadePelaUnidadeDaFase,
                $"A pessoa da posição {indice + 1} não tem autoridade para assinar pela unidade responsável pela fase."),
        };
    }
}
