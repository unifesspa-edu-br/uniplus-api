namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Monta o registro do ato do processo para o ato que cria uma versão de configuração — a abertura e
/// a retificação do edital.
/// </summary>
/// <remarks>
/// O registro nasce junto da versão e da requisição de registro do ato, na mesma transação. Tem o
/// mesmo formato do registro do ato que não cria versão, e é o que dá à consulta dos atos do processo
/// uma fonte única. A unidade e o identificador legível são os do processo no instante da publicação;
/// o documento principal é o do edital e os anexos são os da versão que nasce, na ordem em que o edital os
/// declara — os herdados, quando a retificação não traz lista nova.
/// </remarks>
internal static class RegistroDoAtoDaVersao
{
    public static Result<AtoDoProcesso> Montar(
        ProcessoSeletivo processo,
        VersaoConfiguracao versao,
        DadosDoAto ato,
        TipoAtoPublicadoView tipoConferido,
        DadosEdital dados,
        Guid documentoEditalId,
        Guid? atoRetificadoId,
        string? motivoRetificacao,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(tipoConferido);
        ArgumentNullException.ThrowIfNull(dados);

        return AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                AtoId: versao.AtoCriadorId,
                ProcessoSeletivoId: processo.Id,
                TipoCodigo: ato.TipoAtoCodigo,
                TipoNome: tipoConferido.Nome,
                Numero: dados.Numero,
                DataDoAto: ato.DataPublicacao,
                Assinante: ato.Assinante,
                UnidadeAdministradora: processo.UnidadeAdministradora.Slug,
                IdentificadorLegivelDoProcesso: processo.IdentificadorLegivel!.Value.Valor,
                DocumentoPrincipalId: documentoEditalId,
                Anexos: [.. dados.Anexos.Select(static a => new AnexoDeclarado(a.DocumentoId, a.NomeArquivo))],
                AtoCorrigidoId: atoRetificadoId,
                MotivoDaCorrecao: motivoRetificacao),
            timeProvider);
    }
}
