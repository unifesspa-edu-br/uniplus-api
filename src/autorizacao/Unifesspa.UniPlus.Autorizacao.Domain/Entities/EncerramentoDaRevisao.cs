namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O encerramento avalia as concessões no instante efetivo da revisão, mesmo quando executado depois.
/// Não revoga novamente uma concessão revogada por uma pessoa após esse instante.
/// </summary>
internal sealed class EncerramentoDaRevisao
{
    private readonly RevisaoDasConcessoesDoPerfil _revisao;
    private readonly ContextoDaRevisao _contexto;
    private readonly DateTimeOffset _encerradaEm;

    public EncerramentoDaRevisao(RevisaoDasConcessoesDoPerfil revisao, ContextoDaRevisao contexto, DateTimeOffset encerradaEm)
    {
        _revisao = revisao;
        _contexto = contexto;
        _encerradaEm = encerradaEm;
    }

    public void Encerrar()
    {
        TirarDaLista();
        EncerrarAsPendentes();
    }

    // A concessão que deixou de estar ativa antes do encerramento já não aguarda decisão.
    private void TirarDaLista()
    {
        foreach (MigracaoDaConcessao linha in _revisao.Migracoes.Where(static m => !m.Situacao.EhFinal()).ToList())
        {
            Concessao? concessao = _contexto.Concessao(linha.ConcessaoId);
            if (concessao is null || !concessao.EstavaAtivaEm(_encerradaEm))
            {
                _revisao.RegistrarDoSistema(concessao, linha.ConcessaoId, SituacaoDaMigracao.ForaDaLista, _contexto, _encerradaEm);
            }
        }
    }

    private void EncerrarAsPendentes()
    {
        foreach (Concessao concessao in _contexto.Concessoes.Where(PendenteNoEncerramento).ToList())
        {
            if (concessao.RevogadaEm is null)
            {
                concessao.RevogarPelaMigracao(_revisao.Id, $"não migrado para a versão {_revisao.NumeroDaVersaoNova}", _encerradaEm);
            }

            _revisao.RegistrarDoSistema(concessao, concessao.Id, SituacaoDaMigracao.NaoMigrada, _contexto, _encerradaEm);
        }
    }

    private bool PendenteNoEncerramento(Concessao concessao) =>
        concessao.PerfilId == _revisao.PerfilId
        && concessao.VersaoId != _revisao.VersaoAlvoId
        && concessao.EstavaAtivaEm(_encerradaEm)
        && (_revisao.LinhaDe(concessao.Id) is not { } linha || !linha.Situacao.EhFinal());
}
