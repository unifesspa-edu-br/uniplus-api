namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Avisa quem aprova a etapa em que a versão pendente ficou apta: no prazo, com o documento verificado e a etapa atual
/// calculada como a fila a calcula. Pode ser chamado depois de toda transição que torna uma etapa apta; o aviso de uma
/// mesma versão, ciclo e etapa sai uma vez só.
/// </summary>
public sealed class AvisoDeEtapaApta
{
    private readonly EmissorDeAvisos _emissor;
    private readonly GruposDaGovernanca _grupos;
    private readonly ILinkDaGovernanca _link;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly TimeProvider _clock;

    public AvisoDeEtapaApta(EmissorDeAvisos emissor, GruposDaGovernanca grupos, ILinkDaGovernanca link, CatalogoDaAplicacao catalogo, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(clock);

        _emissor = emissor;
        _grupos = grupos;
        _link = link;
        _catalogo = catalogo;
        _clock = clock;
    }

    public async Task<IReadOnlyList<EnviarEmail>> EmitirAsync(Perfil perfil, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        if (!EstaApta(perfil, out VersaoPendenteDto? pendente))
        {
            return [];
        }

        VersaoDoPerfil versao = perfil.VersaoPendente!;
        EtapaDaVersao etapa = EtapaDaVersaoCodigo.FromCodigo(pendente!.EtapaAtual);
        List<DestinatarioDoAviso> aprovadores = await _grupos.ListarAsync(GruposDaGovernanca.DaEtapa(etapa), cancellationToken).ConfigureAwait(false);
        ModeloDeAviso modelo = new(
            TipoDeAviso.VersaoPendenteApta, perfil.Nome, versao.Numero, [.. pendente.Acrescimo.Select(static p => p.Nome)],
            _link.DaVersao(perfil.Id, versao.Id), Prazo: versao.ExpiraEm, Etapa: etapa);

        return await _emissor
            .EmitirAsync(new PedidoDeAviso(modelo, versao.Id, $"{versao.CicloDeAprovacao}:{etapa.ToCodigo()}", aprovadores, Excluidos(versao)), cancellationToken)
            .ConfigureAwait(false);
    }

    private bool EstaApta(Perfil perfil, out VersaoPendenteDto? pendente)
    {
        pendente = VersaoMapping.PendenteDe(perfil, _catalogo, leitor: null);

        return pendente is not null
            && !perfil.VersaoPendente!.Expirou(_clock.GetUtcNow())
            && perfil.DocumentoDe(perfil.VersaoPendente)?.Situacao == SituacaoDoAnexo.Verificado;
    }

    // Quem criou a versão não aprova a própria, e quem a aprovou pela gestão não a aprova de novo pela proteção de dados.
    private static Identidade[] Excluidos(VersaoDoPerfil versao) =>
        versao.DecisaoDaEtapa(EtapaDaVersao.GestaoDeAcessos) is { Resultado: ResultadoDaDecisao.Aprovado } gestao
            ? [versao.CriadaPor, gestao.Decisor]
            : [versao.CriadaPor];
}
