namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Lê e confere o conteúdo contra o catálogo, e só então cria o modelo, com as recusas do catálogo
/// e as do modelo no mesmo lote (ADR-0125). A entrada que não se lê não chega às regras do conteúdo,
/// que dependem dele inteiro; as do cadastro saem junto.
/// </summary>
public static class CriarModeloFormularioCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarModeloFormularioCommand command,
        IModeloFormularioRepository repository,
        IFatoCandidatoRepository fatoRepository,
        ITermoConsentimentoReader termoReader,
        ITipoProcessoReader tipoProcessoReader,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(termoReader);
        ArgumentNullException.ThrowIfNull(tipoProcessoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        (ConteudoLido conteudo, CatalogoDoModelo catalogo, List<FieldError> erros) = await EscritaDoModelo.LerEConferirAsync(
            command.Conteudo, command.TipoProcessoCodigo, tipoProcessoAnterior: null, VinculosDoModelo.Nenhum,
            fatoRepository, termoReader, tipoProcessoReader, cancellationToken).ConfigureAwait(false);
        FinalidadeFormulario finalidade = EstruturaFormulario.FinalidadeDoToken(command.Finalidade);
        if (conteudo.Erros.Count > 0)
        {
            erros.AddRange(ModeloFormulario.ConferirCadastro(command.Codigo, command.Nome, command.Descricao, command.TipoProcessoCodigo, finalidade));
            return Result<Guid>.ValidationFailure(erros);
        }

        Result<ModeloFormulario> criar = ModeloFormulario.Criar(
            command.Codigo, command.Nome, command.Descricao, finalidade, command.TipoProcessoCodigo, conteudo.ParaConteudo(), catalogo.Derivacoes);
        if (criar.IsFailure)
        {
            erros.AddRange(criar.Errors);
        }

        return erros.Count > 0
            ? Result<Guid>.ValidationFailure(erros)
            : await EscritaDoModelo.GravarNovoAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Como a criação, com o modelo gravado como referência: o vínculo a fato, valor ou tipo de
/// processo desativado só é recusado quando é novo (ADR-0136).
/// </summary>
public static class AtualizarModeloFormularioCommandHandler
{
    public static Task<Result> Handle(
        AtualizarModeloFormularioCommand command,
        IModeloFormularioRepository repository,
        IFatoCandidatoRepository fatoRepository,
        ITermoConsentimentoReader termoReader,
        ITipoProcessoReader tipoProcessoReader,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(termoReader);
        ArgumentNullException.ThrowIfNull(tipoProcessoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        return EscritaDoModelo.MutarAsync(
            command.Id,
            async modelo =>
            {
                (ConteudoLido conteudo, CatalogoDoModelo catalogo, List<FieldError> erros) = await EscritaDoModelo.LerEConferirAsync(
                    command.Conteudo, command.TipoProcessoCodigo, modelo.TipoProcessoCodigo, ConferenciaDoModelo.VinculosGravados(modelo.Conteudo),
                    fatoRepository, termoReader, tipoProcessoReader, cancellationToken).ConfigureAwait(false);
                if (conteudo.Erros.Count > 0)
                {
                    erros.AddRange(ModeloFormulario.ConferirDescritivo(command.Nome, command.Descricao, command.TipoProcessoCodigo));
                    return Result.ValidationFailure(erros);
                }

                Result atualizar = modelo.Atualizar(
                    command.Nome, command.Descricao, command.TipoProcessoCodigo, conteudo.ParaConteudo(), catalogo.Derivacoes);
                if (atualizar.IsFailure)
                {
                    erros.AddRange(atualizar.Errors);
                }

                return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
            },
            repository,
            unitOfWork,
            cancellationToken);
    }
}

public static class AtivarModeloFormularioCommandHandler
{
    public static Task<Result> Handle(
        AtivarModeloFormularioCommand command,
        IModeloFormularioRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        return EscritaDoModelo.MutarAsync(command.Id, static m => Task.FromResult(m.Ativar()), repository, unitOfWork, cancellationToken);
    }
}

public static class DesativarModeloFormularioCommandHandler
{
    public static Task<Result> Handle(
        DesativarModeloFormularioCommand command,
        IModeloFormularioRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        return EscritaDoModelo.MutarAsync(command.Id, static m => Task.FromResult(m.Desativar()), repository, unitOfWork, cancellationToken);
    }
}
