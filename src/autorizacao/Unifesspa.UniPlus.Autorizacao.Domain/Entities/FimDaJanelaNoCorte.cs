namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que o fim da janela faz às concessões, avaliado no corte e não no instante da execução: a rotina que roda atrasada
/// revoga com a data do prazo quem estava válido no prazo e vencera até lá, e não revoga de novo quem uma pessoa revogou
/// depois do corte.
/// </summary>
internal sealed class FimDaJanelaNoCorte
{
    private readonly JanelaDeMigracao _janela;
    private readonly ContextoDaMigracao _contexto;
    private readonly DateTimeOffset _corte;

    public FimDaJanelaNoCorte(JanelaDeMigracao janela, ContextoDaMigracao contexto, DateTimeOffset corte)
    {
        _janela = janela;
        _contexto = contexto;
        _corte = corte;
    }

    public void Encerrar()
    {
        TirarDaLista();
        EncerrarAsPendentes();
    }

    // A linha ainda aberta cuja concessão saiu antes do corte já não é decisão de ninguém.
    private void TirarDaLista()
    {
        foreach (MigracaoDaConcessao linha in _janela.Migracoes.Where(static m => !m.Situacao.EhFinal()).ToList())
        {
            Concessao? concessao = _contexto.Concessao(linha.ConcessaoId);
            if (concessao is null || !concessao.EstavaAtivaEm(_corte))
            {
                _janela.RegistrarDoSistema(concessao, linha.ConcessaoId, SituacaoDaMigracao.ForaDaLista, _contexto, _corte);
            }
        }
    }

    private void EncerrarAsPendentes()
    {
        foreach (Concessao concessao in _contexto.Concessoes.Where(PendenteNoCorte).ToList())
        {
            if (concessao.RevogadaEm is null)
            {
                concessao.RevogarPelaMigracao(_janela.Id, $"não migrado para a versão {_janela.NumeroDaVersaoNova}", _corte);
            }

            _janela.RegistrarDoSistema(concessao, concessao.Id, SituacaoDaMigracao.NaoMigrada, _contexto, _corte);
        }
    }

    private bool PendenteNoCorte(Concessao concessao) =>
        concessao.PerfilId == _janela.PerfilId
        && concessao.VersaoId != _janela.VersaoAlvoId
        && concessao.EstavaAtivaEm(_corte)
        && (_janela.LinhaDe(concessao.Id) is not { } linha || !linha.Situacao.EhFinal());
}
