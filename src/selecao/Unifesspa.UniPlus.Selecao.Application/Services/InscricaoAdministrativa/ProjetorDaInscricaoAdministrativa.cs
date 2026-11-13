namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// A visibilidade de cada classe de dado e de arquivo da inscrição que a pessoa já alcança (ADR-0153), decidida sem olhar
/// o conteúdo e registrada só quando permitida: o registro significa acesso autorizado à classe naquela inscrição, e não
/// dado entregue. Quem não pode ver um item o recebe omitido, sem indicação.
/// </summary>
public sealed class ProjetorDaInscricaoAdministrativa
{
    private static readonly (NaturezaDoItem, ClassificacaoDeProtecao)[] ClassesPedidas =
    [
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Publico),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Interno),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Pessoal),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Identificador),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Sensivel),
    ];

    private readonly IVerificadorDeAcesso _verificador;
    private readonly ProjetorDeItens _projetor;
    private readonly IPortaDePermissaoPorPar _porta;

    public ProjetorDaInscricaoAdministrativa(IVerificadorDeAcesso verificador, ProjetorDeItens projetor, IPortaDePermissaoPorPar porta)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(projetor);
        ArgumentNullException.ThrowIfNull(porta);

        _verificador = verificador;
        _projetor = projetor;
        _porta = porta;
    }

    /// <summary>
    /// Decide a visibilidade de todas as classes de dado e de arquivo e a capacidade de revelar identificador, e registra as
    /// classes permitidas. Só depois de gravados os registros a requisição segue.
    /// </summary>
    public async Task<ProjecaoDaInscricao> ProjetarAsync(ResourceContext resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);

        DecisoesDaProjecao decisoes = await _projetor
            .DecidirAsync(_porta, resource, ClassesPedidas, cancellationToken).ConfigureAwait(false);
        bool podeRevelar = decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador)
            && await PodeAsync(PermissaoSelecao.DadosIdentificadoresRevelar, resource, cancellationToken).ConfigureAwait(false);
        await _projetor.RegistrarAsync(decisoes, cancellationToken).ConfigureAwait(false);

        return new ProjecaoDaInscricao(decisoes, podeRevelar);
    }

    /// <summary>Se a pessoa tem a permissão de emitir o comprovante da inscrição; consulta de capacidade, que não grava registro.</summary>
    public Task<bool> PodeEmitirComprovanteAsync(ResourceContext resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return PodeAsync(PermissaoSelecao.InscricoesEmitirComprovante, resource, cancellationToken);
    }

    private async Task<bool> PodeAsync(PermissaoSelecao permissao, ResourceContext resource, CancellationToken cancellationToken) =>
        await _verificador.PodeAsync(permissao.Requisito, resource, cancellationToken).ConfigureAwait(false) == ResultadoDoAcesso.Permitido;
}
