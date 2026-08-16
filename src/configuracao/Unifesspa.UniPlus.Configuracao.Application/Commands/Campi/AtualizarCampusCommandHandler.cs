namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// Ordem de checagens, todas antes de qualquer I/O que não seja estritamente
/// necessário: campo + endereço (sem tocar o repositório) → existência do
/// Campus → unicidade de sigla → mutação. Antes da ADR-0125, o validator
/// FluentValidation rodava como middleware antes do handler, então um payload
/// mal formado nunca chegava a <c>ObterPorIdAsync</c> — a validação sempre
/// vencia sobre "não encontrado". Para preservar essa propriedade sem o
/// validator, campo e endereço são validados com <see cref="Campus.ValidarAtualizacao"/>
/// (estático, sem instância) antes de buscar o agregado; só um comando já
/// confirmado válido chega a consultar a existência ou a unicidade. Como o
/// Campus chega rastreado pelo EF (via <c>ObterPorIdAsync</c>) e o Wolverine
/// roda <c>SaveChangesAsync</c> depois do handler retornar mesmo quando o
/// <see cref="Result"/> é falha, a mutação (<see cref="Campus.Atualizar"/>) só
/// acontece por último, depois de confirmar também a unicidade.
/// </remarks>
public static class AtualizarCampusCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarCampusCommand command,
        ICampusRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset agora = timeProvider.GetUtcNow();

        // Resolvido sem "existente" — a essa altura ainda não se sabe se o Campus
        // existe. O erro de formato/coerência do endereço não depende de
        // "existente" (só a escolha de instância entre novo/preservado depende,
        // e essa otimização é refeita mais abaixo, já com o agregado em mãos).
        (DomainError? enderecoErro, ReferenciaEnderecoGeo? enderecoValidado) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);

        Result validacaoCampos = Campus.ValidarAtualizacao(
            command.Sigla,
            command.Nome,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            enderecoErro is null ? enderecoValidado : null,
            command.CodigoEmec);

        if (enderecoErro is not null || validacaoCampos.IsFailure)
        {
            List<FieldError> erros = [.. validacaoCampos.Errors];
            if (enderecoErro is not null)
            {
                erros.Add(new FieldError("endereco", enderecoErro));
            }

            return Result.ValidationFailure(erros);
        }

        Campus? campus = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (campus is null)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.NaoEncontrado,
                "Campus não encontrado."));
        }

        string siglaAntiga = campus.Sigla;

        // Só recarimba a proveniência/frescura do display cache quando o trio de
        // cidade efetivamente muda — decidido com os valores antigos, antes de
        // Atualizar mutar o agregado.
        bool cidadeMudou = CidadeReferenciaMudou(command, campus);
        string? cidadeOrigem = cidadeMudou ? ReferenciaCidadeGeo.OrigemGeoApi : campus.CidadeOrigem;
        DateTimeOffset? cidadeAtualizadoEm = cidadeMudou ? agora : campus.CidadeDisplayAtualizadoEm;

        // Re-resolve agora com o endereço existente real, só para a otimização de
        // preservar o carimbo do display cache quando o conteúdo não muda — o
        // resultado de erro já é conhecido (mesmo input, mesmo agora) e não pode
        // divergir do calculado acima, então não há necessidade de validar de novo.
        (_, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, campus.Endereco, agora);

        // Unicidade consultada com a sigla já normalizada (mesma transformação que
        // Atualizar aplicaria), sem mutar o agregado rastreado ainda — só consulta
        // o repositório quando a sigla de fato muda.
        string siglaNormalizada = command.Sigla!.Trim().ToUpperInvariant();
        if (!string.Equals(siglaAntiga, siglaNormalizada, StringComparison.OrdinalIgnoreCase)
            && await repository.SiglaExisteEntreLivosAsync(siglaNormalizada, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.SiglaJaExiste,
                $"Já existe um Campus vivo com a sigla '{siglaNormalizada}'."));
        }

        // Só muta o agregado rastreado pelo EF depois de confirmar campos e
        // unicidade — Atualizar valida de novo internamente (sem I/O, barato) com
        // exatamente os mesmos argumentos já confirmados acima, então sempre
        // terá sucesso aqui; esta chamada só serve para aplicar a mutação.
        campus.Atualizar(
            command.Sigla,
            command.Nome,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            cidadeOrigem,
            cidadeAtualizadoEm,
            endereco,
            command.CodigoEmec);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // Null-safe (?.Trim()) de propósito: um comando com cidade nula/ausente ainda
    // precisa chegar a campus.Atualizar para virar a violação "obrigatório" do
    // domínio — não pode estourar NullReferenceException aqui antes disso.
    private static bool CidadeReferenciaMudou(AtualizarCampusCommand command, Campus campus) =>
        !string.Equals(command.CidadeCodigoIbge?.Trim(), campus.CidadeCodigoIbge, StringComparison.Ordinal)
        || !string.Equals(command.CidadeNome?.Trim(), campus.CidadeNome, StringComparison.Ordinal)
        || !string.Equals(command.CidadeUf?.Trim(), campus.CidadeUf, StringComparison.OrdinalIgnoreCase);
}
