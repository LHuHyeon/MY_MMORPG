using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class PlayerController : BaseController
{
    bool _stopAttack = true;   // 공격 가능 여부
    [SerializeField]
    bool _isDiveRoll = false;    // 구르기 여부

    // LayerMask 변수
    int _mask = (1 << (int)Define.Layer.Ground) | (1 << (int)Define.Layer.Monster) | (1 << (int)Define.Layer.Npc);

    [SerializeField]
    private GameObject skillEffect1;
    [SerializeField]
    private GameObject skillEffect2;

    private GameObject boxColider;

    public override void Init()
    {
        WorldObjectType = Define.WorldObject.Player;
        
        anim = GetComponent<Animator>();

        boxColider = transform.GetChild(0).gameObject;

        Managers.Input.KeyAction -= OnKeyEvent;
        Managers.Input.KeyAction += OnKeyEvent;
        Managers.Input.MouseAction -= OnMouseEvent;
        Managers.Input.MouseAction += OnMouseEvent;
    }

    protected override void UpdateIdle()
    {
        // TODO : 가만히 있을때의 모션 있으면 사용
        if (_stopAttack == false)
            StopAttack();
    }

    Vector3 dir;
    protected override void UpdateMoving()
    {
        if (_lockTarget != null){
            float distance = (_lockTarget.transform.position - transform.position).magnitude;
            if (distance <= 1f){
                // TODO : npc 상호작용
                State = Define.State.Idle;
                return;
            }
        }

        // 타겟 대상을 클릭했을 때 콜라이더 위쪽을 클릭하게 된다면 그쪽을 바라보고 달리기 때문에 y값을 0으로 준다.
        _destPos.y = 0; 

        // 도착 위치 벡터에서 플레이어 위치 벡터를 뺀다.
        dir = _destPos - transform.position;

        // Vector3.magnitude = 벡터값의 길이
        if (dir.magnitude < 0.1f)
            State = Define.State.Idle;
        else
        {
            // 건물을 클릭하여 이동하면 건물 앞에 멈추기 (1.0f 거리에서 멈추기)
            Debug.DrawRay(transform.position + (Vector3.up * 0.5f), dir.normalized, Color.red);
            if (Physics.Raycast(transform.position + (Vector3.up * 0.5f), dir, 1.0f, LayerMask.GetMask("Block"))){
                if (Input.GetMouseButton(0) == false)
                    State = Define.State.Idle;
                return;
            }

            float moveDist = Mathf.Clamp(Managers.Game.MoveSpeed * Time.deltaTime, 0, dir.magnitude);
            
            transform.position += dir.normalized * moveDist;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 20f * Time.deltaTime);
        }
    }

    protected override void UpdateDiveRoll()
    {
        StopAttack();

        _destPos = GetMousePoint();

        float moveDist = Mathf.Clamp(Managers.Game.MoveSpeed * Time.deltaTime, 0, dir.magnitude);

        transform.position += dir.normalized * moveDist;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 20f * Time.deltaTime);
    }

    float _attackCloseTime = 0;
    protected override void UpdateAttack()
    {
        _attackCloseTime += Time.deltaTime;

        if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Attack") &&
            _attackCloseTime > 0.94f &&
            _onComboAttack == false)
        {
            StopAttack();
            State = Define.State.Idle;
            return;
        }

        dir = _destPos - transform.position;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 20f * Time.deltaTime);
    }

    protected override void UpdateSkill()
    {
        if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Skill") &&
            anim.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.9f)
        {
            skillEffect1.SetActive(false);
            skillEffect2.SetActive(false);

            State = Define.State.Idle;
        }
    }

    // 마우스 클릭
    void OnMouseEvent(Define.MouseEvent evt)
    {
        switch(State){
            case Define.State.Moving:
                GetMouseEvent(evt);
                break;
            case Define.State.Idle:
                GetMouseEvent(evt);
                break;
            case Define.State.Attack:
                GetMouseEvent(evt);
                break;
            case Define.State.Skill:
                {
                    // TODO : 스킬 진행 중일 때 마우스 입력한다면 스킬이 끝날때까지 마우스 입력 못함.
                    // TODO : 스페이스바로 구르기 가능 
                }
                break;
        }
    }

    void GetMouseEvent(Define.MouseEvent evt)
    {
        // 메인 카메라에서 마우스가 가르키는 위치의 ray를 저장
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool raycastHit = Physics.Raycast(ray, out hit, 150f, _mask);
        
        switch (evt)
        {
            // 마우스를 클릭했을 때 [ 클릭한 위치로 이동 ]
            case Define.MouseEvent.RightDown:
                {
                    _destPos = hit.point;   // 해당 좌표 저장
                    if (raycastHit && _stopAttack)
                    {
                        State = Define.State.Moving;

                        if (hit.collider.gameObject.layer == (int)Define.Layer.Npc)
                            _lockTarget = hit.collider.gameObject;
                        else
                            _lockTarget = null;
                    }
                }
                break;
            // 마우스를 클릭 중일 때
            case Define.MouseEvent.RightPress:
                {
                    if (_stopAttack)
                    {
                        if (State == Define.State.Idle)
                            State = Define.State.Moving;

                        if (_lockTarget != null)
                            _destPos = _lockTarget.transform.position;
                        else if (raycastHit)
                            _destPos = hit.point;
                    }
                }
                break;
            // 왼쪽 클릭 시 공격
            case Define.MouseEvent.LeftDown:
                {
                    _stopAttack = false;
                    _destPos = hit.point;
                    _destPos.y = 0;
                    OnAttack();
                }
                break;
            case Define.MouseEvent.LeftPress:
                {
                    _destPos = hit.point;
                    _destPos.y = 0;
                    OnAttack();
                }
                break;
        }
    }

    bool _onAttack = false;
    bool _onComboAttack = false;
    int attackClipNumber = 0;
    string[] _attackClipList = new string[] {"ATTACK1", "ATTACK2"};
    void OnAttack()
    {
        // 콤보 체크
        if (_onAttack &&
            anim.GetCurrentAnimatorStateInfo(0).IsTag("Attack") &&
            anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.3f)
        {
            _onComboAttack = true;
        }

        // 공격!
        if (!_onAttack)
        {
            anim.CrossFade(_attackClipList[attackClipNumber], 0.1f, -1, 0);
            State = Define.State.Attack;

            _onAttack = true;
        }
    }

    // [ Anim Event ]
    public void ExitAttack()
    {
        // boxColider.SetActive(false);
        if (_onComboAttack == true)
        {
            if (attackClipNumber == 1)
                attackClipNumber = 0;
            else if (attackClipNumber == 0)
                attackClipNumber = 1;

            anim.CrossFade(_attackClipList[attackClipNumber], 0.1f, -1, 0);
            _onComboAttack = false;
        }
    }

    void StopAttack()
    {
        _onAttack = false;
        _onComboAttack = false;
        _stopAttack = true;
        _attackCloseTime = 0;
        attackClipNumber = 0;
    }

    // 키보드 클릭
    void OnKeyEvent()
    {
        if (_isDiveRoll == false)
        {
            GetDiveRoll();
            GetSkill();
        }
    }

    // [ Anim Event ]
    public void EventDiveRoll()
    {
        _isDiveRoll = false;
        Managers.Game.MoveSpeed = 5;
        State = Define.State.Idle;
    }

    void GetDiveRoll()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _isDiveRoll = true;
            State = Define.State.DiveRoll;

            _destPos = GetMousePoint();
            dir = _destPos - transform.position;
            
            Managers.Game.MoveSpeed = 8;

            skillEffect1.SetActive(false);
            skillEffect2.SetActive(false);
        }
    }

    // 스킬 사용
    void GetSkill()
    {
        // TODO : 스킬 관리코드 구현 (확장성)
        if (State == Define.State.Skill)
            return;

        _destPos = GetMousePoint();

        dir = _destPos - transform.position;
        transform.rotation = Quaternion.LookRotation(dir);

        if (Input.GetKeyDown(KeyCode.Q))
        {
            State = Define.State.Skill;
            anim.CrossFade("COMBO_1", 0.1f, -1, 0);
            skillEffect1.SetActive(true);
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            State = Define.State.Skill;
            anim.CrossFade("COMBO_2", 0.1f, -1, 0);
            skillEffect2.SetActive(true);
        }
    }

    // 마우스 Ray
    public Vector3 GetMousePoint()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out hit, 150f, _mask);
        
        Vector3 hitPoint = hit.point;
        hitPoint.y = 0;
        return hitPoint;
    }
}
